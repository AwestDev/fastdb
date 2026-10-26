using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure.Maps;

/// <summary>
/// Picks the reader from the file. Status per format:
///  .mbtiles  fully supported (vector + raster).
///  .gmdb     GMap.NET tile cache (Tiles + TilesData, raster) via GmdbMapSource; a file with the MBTiles layout
///            still opens; anything else reports its table list.
///  .pbf      OpenStreetMap raw data is not a tile format: reported with conversion guidance (converter = next phase).
/// </summary>
public sealed class MapSourceFactory : IMapSourceFactory
{
    public async Task<IMapSource> OpenAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Map file not found.", path);

        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".mbtiles":
                return await MbTilesMapSource.OpenAsync(path, "MBTiles", ct);
            case ".gmdb":
                // Real GMap.NET cache first (Tiles + TilesData); otherwise fall back to the MBTiles layout.
                var gmdb = await GmdbMapSource.TryOpenAsync(path, ct);
                if (gmdb is not null) return gmdb;
                return await MbTilesMapSource.OpenAsync(path, "GMDB", ct);
            case ".pbf":
                if (LooksLikeOsmPbf(path))
                    throw new UnsupportedMapFormatException(UnsupportedMapReason.OsmPbfNeedsConversion,
                        $"OpenStreetMap data file, {new FileInfo(path).Length / (1024 * 1024):N0} MB.");
                throw new UnsupportedMapFormatException(UnsupportedMapReason.UnknownFormat, "Unrecognised .pbf content.");
            default:
                throw new UnsupportedMapFormatException(UnsupportedMapReason.UnknownFormat,
                    $"Extension '{Path.GetExtension(path)}' is not supported.");
        }
    }

    // OSM PBF starts with a BlobHeader whose type string is "OSMHeader".
    private static bool LooksLikeOsmPbf(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[128];
        var read = stream.Read(buffer, 0, buffer.Length);
        return System.Text.Encoding.ASCII.GetString(buffer, 0, read).Contains("OSMHeader", StringComparison.Ordinal);
    }
}
