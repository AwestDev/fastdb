using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure.Maps;
using Microsoft.Data.Sqlite;

namespace FastDbExplorer.Tests;

public class MvtLayerReaderTests
{
    // Tile { layers { name = "water", version = 2 } } written by hand.
    private static readonly byte[] WaterTile = [0x1A, 0x09, 0x0A, 0x05, (byte)'w', (byte)'a', (byte)'t', (byte)'e', (byte)'r', 0x78, 0x02];

    [Fact]
    public void Reads_layer_names() => Assert.Equal(["water"], MvtLayerReader.ReadLayerNames(WaterTile));

    [Fact]
    public void Garbage_does_not_throw() => Assert.Empty(MvtLayerReader.ReadLayerNames([0xFF, 0xFF, 0xFF]));
}

public class MapSourceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fde-map-" + Guid.NewGuid());
    private readonly MapSourceFactory _factory = new();
    private const string WaterTileHex = "X'1A090A0577617465727802'";

    private string Make(string fileName, params string[] sql)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, fileName);
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            foreach (var statement in sql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
        }
        SqliteConnection.ClearAllPools();
        return path;
    }

    private static string[] Mbtiles(params string[] extra) =>
    [
        "CREATE TABLE metadata (name TEXT, value TEXT)",
        "CREATE TABLE tiles (zoom_level INTEGER, tile_column INTEGER, tile_row INTEGER, tile_data BLOB)",
        .. extra
    ];

    [Fact]
    public async Task Opens_vector_mbtiles_and_flips_tms_rows()
    {
        var path = Make("a.mbtiles", Mbtiles(
            "INSERT INTO metadata VALUES ('format','pbf'), ('minzoom','0'), ('maxzoom','5'), ('bounds','44.0,25.0,63.5,39.8'), " +
            "('json','{\"vector_layers\":[{\"id\":\"water\"},{\"id\":\"roads\"}]}')",
            $"INSERT INTO tiles VALUES (1, 0, 1, {WaterTileHex})"));

        using var source = await _factory.OpenAsync(path, CancellationToken.None);

        Assert.Equal(MapTileKind.Vector, source.Info.Kind);
        Assert.Equal(["water", "roads"], source.Info.Layers);
        Assert.Equal(4, source.Info.Bounds!.Length);
        Assert.NotNull(await source.GetTileAsync(1, 0, 0, CancellationToken.None)); // XYZ y=0 == TMS row 1
        Assert.Null(await source.GetTileAsync(1, 0, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Finds_layers_inside_tiles_when_metadata_is_missing()
    {
        var path = Make("b.mbtiles", Mbtiles($"INSERT INTO tiles VALUES (0, 0, 0, {WaterTileHex})"));

        using var source = await _factory.OpenAsync(path, CancellationToken.None);

        Assert.Equal(["water"], source.Info.Layers);
        Assert.Equal((0, 0), (source.Info.MinZoom, source.Info.MaxZoom));
    }

    [Fact]
    public async Task Gmdb_with_mbtiles_layout_opens_and_is_labelled_gmdb()
    {
        var path = Make("c.gmdb", Mbtiles($"INSERT INTO tiles VALUES (0, 0, 0, {WaterTileHex})"));

        using var source = await _factory.OpenAsync(path, CancellationToken.None);

        Assert.Equal("GMDB", source.Info.FormatLabel);
    }

    [Fact]
    public async Task Gmdb_gmap_cache_opens_as_raster_without_flipping_y()
    {
        var path = Make("e.gmdb",
            "CREATE TABLE Tiles (id INTEGER NOT NULL PRIMARY KEY, X INTEGER NOT NULL, Y INTEGER NOT NULL, Zoom INTEGER NOT NULL, Type INTEGER NOT NULL, CacheTime DATETIME)",
            "CREATE TABLE TilesData (id INTEGER NOT NULL PRIMARY KEY, Tile BLOB NULL)",
            "INSERT INTO Tiles VALUES (1, 5, 3, 3, 77, NULL), (2, 0, 0, 0, 77, NULL), (3, 9, 9, 3, 99, NULL)",
            "INSERT INTO TilesData VALUES (1, X'89504E470D0A1A0A'), (2, X'89504E470D0A1A0A'), (3, X'FFD8FFE0')");

        using var source = await _factory.OpenAsync(path, CancellationToken.None);

        Assert.Equal("GMDB", source.Info.FormatLabel);
        Assert.Equal(MapTileKind.Raster, source.Info.Kind);
        Assert.Equal((0, 3), (source.Info.MinZoom, source.Info.MaxZoom));
        Assert.Equal(4, source.Info.Bounds!.Length);
        var tile = await source.GetTileAsync(3, 5, 3, CancellationToken.None);
        Assert.Equal("image/png", tile!.ContentType);
        Assert.Null(await source.GetTileAsync(3, 9, 9, CancellationToken.None)); // other provider (Type 99) is ignored
    }

    [Fact]
    public async Task Gmdb_with_unknown_schema_reports_its_tables()
    {
        var path = Make("d.gmdb", "CREATE TABLE places (id INTEGER, geom BLOB)");

        var ex = await Assert.ThrowsAsync<UnsupportedMapFormatException>(() => _factory.OpenAsync(path, CancellationToken.None));

        Assert.Equal(UnsupportedMapReason.UnknownSqliteSchema, ex.Reason);
        Assert.Contains("places", ex.Message);
    }

    [Fact]
    public async Task Osm_pbf_is_recognised_and_explained()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "region.osm.pbf");
        await File.WriteAllBytesAsync(path, [0, 0, 0, 13, 10, 9, .. "OSMHeader"u8.ToArray(), 24, 5]);

        var ex = await Assert.ThrowsAsync<UnsupportedMapFormatException>(() => _factory.OpenAsync(path, CancellationToken.None));

        Assert.Equal(UnsupportedMapReason.OsmPbfNeedsConversion, ex.Reason);
    }

    [Fact]
    public async Task Non_sqlite_mbtiles_is_unknown_format()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "bad.mbtiles");
        await File.WriteAllTextAsync(path, "this is not sqlite at all");

        var ex = await Assert.ThrowsAsync<UnsupportedMapFormatException>(() => _factory.OpenAsync(path, CancellationToken.None));

        Assert.Equal(UnsupportedMapReason.UnknownFormat, ex.Reason);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
