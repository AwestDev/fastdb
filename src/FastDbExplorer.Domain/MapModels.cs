namespace FastDbExplorer.Domain;

public enum MapTileKind { Vector, Raster }

/// <summary>Everything the viewer needs to know about an opened map file.</summary>
public sealed record MapSourceInfo(
    string Name,
    string FormatLabel,
    MapTileKind Kind,
    int MinZoom,
    int MaxZoom,
    double[]? Bounds,      // west, south, east, north (degrees)
    double[]? Center,      // lon, lat, zoom
    IReadOnlyList<string> Layers);

/// <summary>One tile as stored in the file. IsGzip tells the viewer to send "Content-Encoding: gzip".</summary>
public sealed record MapTile(byte[] Data, string ContentType, bool IsGzip);

public enum UnsupportedMapReason { OsmPbfNeedsConversion, UnknownSqliteSchema, UnknownFormat }

/// <summary>The file is understood well enough to explain why it cannot be shown (not a crash).</summary>
public sealed class UnsupportedMapFormatException(UnsupportedMapReason reason, string details) : Exception(details)
{
    public UnsupportedMapReason Reason { get; } = reason;
}

/// <summary>The file is supposed to be supported but is damaged or unreadable.</summary>
public sealed class MapSourceException(string message, Exception? inner = null) : Exception(message, inner);
