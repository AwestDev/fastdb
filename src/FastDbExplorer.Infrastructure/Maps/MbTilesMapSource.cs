using System.Globalization;
using System.Text.Json;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using Microsoft.Data.Sqlite;

namespace FastDbExplorer.Infrastructure.Maps;

/// <summary>
/// MBTiles (SQLite) reader, vector (pbf) or raster (png/jpg/webp). Opened read-only.
/// MBTiles stores rows in TMS order, so the viewer's XYZ y is flipped: tms_row = 2^z - 1 - y.
/// </summary>
public sealed class MbTilesMapSource : IMapSource
{
    private readonly string _connectionString;
    private readonly string _contentType;

    private MbTilesMapSource(string connectionString, MapSourceInfo info, string contentType)
    {
        _connectionString = connectionString;
        _contentType = contentType;
        Info = info;
    }

    public MapSourceInfo Info { get; }

    public static Task<MbTilesMapSource> OpenAsync(string path, string formatLabel, CancellationToken ct)
        => Task.Run(() => Open(path, formatLabel), ct);

    private static MbTilesMapSource Open(string path, string formatLabel)
    {
        if (!HasSqliteHeader(path))
            throw new UnsupportedMapFormatException(UnsupportedMapReason.UnknownFormat, "The file is not a SQLite database.");

        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var tables = Scalars(connection, "SELECT name FROM sqlite_master WHERE type IN ('table','view') ORDER BY name");
            if (!tables.Contains("tiles", StringComparer.OrdinalIgnoreCase))
                throw new UnsupportedMapFormatException(UnsupportedMapReason.UnknownSqliteSchema,
                    "Tables found: " + (tables.Count == 0 ? "(none)" : string.Join(", ", tables)));

            var meta = tables.Contains("metadata", StringComparer.OrdinalIgnoreCase)
                ? ReadMetadata(connection)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var (minZoom, maxZoom) = ReadZoomRange(connection, meta);
            var format = (meta.GetValueOrDefault("format") ?? SniffFormat(connection)).ToLowerInvariant();
            if (format == "jpeg") format = "jpg";

            var kind = format == "pbf" || format == "mvt" ? MapTileKind.Vector : MapTileKind.Raster;
            var contentType = format switch
            {
                "png" => "image/png",
                "jpg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "application/x-protobuf"
            };

            var layers = kind == MapTileKind.Vector ? ReadLayers(connection, meta, minZoom, maxZoom) : [];
            var name = meta.GetValueOrDefault("name") is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(path);
            var info = new MapSourceInfo(name, formatLabel, kind, minZoom, maxZoom,
                ParseDoubles(meta.GetValueOrDefault("bounds"), 4), ParseDoubles(meta.GetValueOrDefault("center"), 2), layers);

            return new MbTilesMapSource(connectionString, info, contentType);
        }
        catch (SqliteException ex)
        {
            throw new MapSourceException("The map file could not be read: " + ex.Message, ex);
        }
    }

    public async Task<MapTile?> GetTileAsync(int zoom, int x, int y, CancellationToken ct)
    {
        if (zoom is < 0 or > 30 || x < 0 || y < 0) return null;
        var tmsRow = (1L << zoom) - 1 - y;
        if (tmsRow < 0) return null;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $y LIMIT 1";
        command.Parameters.AddWithValue("$z", zoom);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$y", tmsRow);

        var result = await command.ExecuteScalarAsync(ct);
        return result is byte[] { Length: > 0 } data ? new MapTile(data, _contentType, MvtLayerReader.IsGzip(data)) : null;
    }

    public void Dispose() => SqliteConnection.ClearAllPools(); // releases the file lock

    private static bool HasSqliteHeader(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[16];
        return stream.Read(header, 0, 16) == 16 && System.Text.Encoding.ASCII.GetString(header) == "SQLite format 3\0";
    }

    private static List<string> Scalars(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    private static Dictionary<string, string> ReadMetadata(SqliteConnection connection)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, value FROM metadata";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1)) meta[reader.GetString(0)] = reader.GetString(1);
        return meta;
    }

    private static (int Min, int Max) ReadZoomRange(SqliteConnection connection, Dictionary<string, string> meta)
    {
        if (int.TryParse(meta.GetValueOrDefault("minzoom"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
            && int.TryParse(meta.GetValueOrDefault("maxzoom"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max))
            return (min, max);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MIN(zoom_level), MAX(zoom_level) FROM tiles";
        using var reader = command.ExecuteReader();
        if (reader.Read() && !reader.IsDBNull(0)) return (reader.GetInt32(0), reader.GetInt32(1));
        throw new MapSourceException("The map file contains no tiles.");
    }

    private static string SniffFormat(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tile_data FROM tiles LIMIT 1";
        if (command.ExecuteScalar() is not byte[] d || d.Length < 12) return "pbf";
        if (d[0] == 0x89 && d[1] == 0x50) return "png";
        if (d[0] == 0xFF && d[1] == 0xD8) return "jpg";
        if (d[0] == 'R' && d[1] == 'I' && d[8] == 'W' && d[9] == 'E') return "webp";
        return "pbf";
    }

    private static List<string> ReadLayers(SqliteConnection connection, Dictionary<string, string> meta, int minZoom, int maxZoom)
    {
        var names = new List<string>();
        if (meta.TryGetValue("json", out var json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("vector_layers", out var array) && array.ValueKind == JsonValueKind.Array)
                    foreach (var layer in array.EnumerateArray())
                        if (layer.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } s) names.Add(s);
            }
            catch (JsonException) { /* fall back to sampling tiles */ }
        }
        if (names.Count > 0) return names;

        // No usable metadata: look inside a few tiles at low, middle and high zoom.
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var zoom in new[] { minZoom, (minZoom + maxZoom) / 2, maxZoom }.Distinct())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT tile_data FROM tiles WHERE zoom_level = $z LIMIT 8";
            command.Parameters.AddWithValue("$z", zoom);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                if (reader.GetValue(0) is byte[] data)
                    foreach (var name in MvtLayerReader.ReadLayerNames(data)) found.Add(name);
        }
        return found.ToList();
    }

    private static double[]? ParseDoubles(string? text, int minCount)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new List<double>();
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
            values.Add(v);
        }
        return values.Count >= minCount ? values.ToArray() : null;
    }
}
