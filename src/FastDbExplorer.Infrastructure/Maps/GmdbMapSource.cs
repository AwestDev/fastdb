using System.Text;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using Microsoft.Data.Sqlite;

namespace FastDbExplorer.Infrastructure.Maps;

/// <summary>
/// GMap.NET tile cache (.gmdb): SQLite with Tiles(id, X, Y, Zoom, Type, CacheTime) and TilesData(id, Tile BLOB).
/// Raster images only (PNG/JPEG). X/Y are already XYZ (y = 0 at the top), so unlike MBTiles there is no flip.
/// One file can hold several map providers (column Type); the provider with the most tiles is shown.
/// </summary>
public sealed class GmdbMapSource : IMapSource
{
    private readonly string _connectionString;
    private readonly long _type;

    private GmdbMapSource(string connectionString, long type, MapSourceInfo info)
    {
        _connectionString = connectionString;
        _type = type;
        Info = info;
    }

    public MapSourceInfo Info { get; }

    /// <summary>Returns null when the file is not a GMap.NET cache (so the caller can try another reader).</summary>
    public static Task<GmdbMapSource?> TryOpenAsync(string path, CancellationToken ct)
        => Task.Run(() => Open(path), ct);

    private static GmdbMapSource? Open(string path)
    {
        if (!HasSqliteHeader(path)) return null;

        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            if (!HasTable(connection, "Tiles") || !HasTable(connection, "TilesData")) return null;

            long type;
            int minZoom, maxZoom;
            using (var command = connection.CreateCommand())
            {
                // One pass over the small Tiles table (the heavy image data lives in TilesData).
                command.CommandText = "SELECT Type, COUNT(*) AS n, MIN(Zoom), MAX(Zoom) FROM Tiles GROUP BY Type ORDER BY n DESC LIMIT 1";
                using var reader = command.ExecuteReader();
                if (!reader.Read() || reader.IsDBNull(2))
                    throw new MapSourceException("The map file contains no tiles.");
                type = reader.GetInt64(0);
                minZoom = reader.GetInt32(2);
                maxZoom = reader.GetInt32(3);
            }

            var bounds = ReadBounds(connection, type, maxZoom);
            var info = new MapSourceInfo(Path.GetFileNameWithoutExtension(path), "GMDB", MapTileKind.Raster,
                minZoom, maxZoom, bounds, null, []);
            return new GmdbMapSource(connectionString, type, info);
        }
        catch (SqliteException ex)
        {
            throw new MapSourceException("The map file could not be read: " + ex.Message, ex);
        }
    }

    public async Task<MapTile?> GetTileAsync(int zoom, int x, int y, CancellationToken ct)
    {
        if (zoom is < 0 or > 30 || x < 0 || y < 0) return null;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT d.Tile FROM Tiles t JOIN TilesData d ON d.id = t.id " +
            "WHERE t.X = $x AND t.Y = $y AND t.Zoom = $z AND t.Type = $type LIMIT 1";
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", y);
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$type", _type);

        var result = await command.ExecuteScalarAsync(ct);
        return result is byte[] { Length: > 0 } data ? new MapTile(data, ContentTypeOf(data), false) : null;
    }

    public void Dispose() => SqliteConnection.ClearAllPools(); // releases the file lock

    // The area that has the deepest zoom is where the real detail is: "fit to map" goes there.
    private static double[]? ReadBounds(SqliteConnection connection, long type, int zoom)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MIN(X), MAX(X), MIN(Y), MAX(Y) FROM Tiles WHERE Type = $type AND Zoom = $z";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$z", zoom);
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0)) return null;

        double minX = reader.GetInt64(0), maxX = reader.GetInt64(1), minY = reader.GetInt64(2), maxY = reader.GetInt64(3);
        return [Lon(minX, zoom), Lat(maxY + 1, zoom), Lon(maxX + 1, zoom), Lat(minY, zoom)]; // west, south, east, north
    }

    private static double Lon(double x, int zoom) => x / Math.Pow(2, zoom) * 360.0 - 180.0;

    private static double Lat(double y, int zoom)
    {
        var n = Math.PI * (1 - 2 * y / Math.Pow(2, zoom));
        return Math.Atan(Math.Sinh(n)) * 180.0 / Math.PI;
    }

    private static bool HasTable(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name COLLATE NOCASE LIMIT 1";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() is not null;
    }

    private static string ContentTypeOf(byte[] d)
    {
        if (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8) return "image/jpeg";
        if (d.Length > 11 && d[0] == 'R' && d[1] == 'I' && d[8] == 'W' && d[9] == 'E') return "image/webp";
        if (d.Length > 3 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F') return "image/gif";
        return "image/png";
    }

    private static bool HasSqliteHeader(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[16];
        return stream.Read(header, 0, 16) == 16 && Encoding.ASCII.GetString(header) == "SQLite format 3\0";
    }
}
