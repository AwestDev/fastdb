namespace FastDbExplorer.Domain;

/// <summary>Where a map layer comes from. The kind decides the marker shape and colour, so the user can tell the sources apart at a glance.</summary>
public enum MapLayerKind
{
    /// <summary>Imported from an Excel file: round marker, icon and colour chosen by the user.</summary>
    Excel,

    /// <summary>Points the user added by hand (typed or picked coordinates): pin marker, layer "my points".</summary>
    Manual,

    /// <summary>Rows sent from the Explorer: amber diamond with a magnifier. Temporary, never saved.</summary>
    Explorer,

    /// <summary>Rows sent from Monitoring: rose rounded square with a bell. Temporary, never saved.</summary>
    Monitoring
}

/// <summary>Fixed look of the non-Excel sources. Pure data (no UI types): the host turns it into the message for the map page.</summary>
public static class MapSourceStyles
{
    public const string ExplorerColor = "#F59E0B";
    public const string MonitoringColor = "#E11D48";
    public const string ManualColor = "#0EA5E9";

    // Glyphs (24x24 grid, even-odd fill), drawn in white inside the marker.
    private const string SearchGlyph =
        "M10 3 a7 7 0 1 0 0 14 a7 7 0 1 0 0 -14 z M10 5.5 a4.5 4.5 0 1 1 0 9 a4.5 4.5 0 1 1 0 -9 z M14.6 16 l1.4 -1.4 l5.4 5.4 l-1.4 1.4 z";

    private const string BellGlyph =
        "M12 2.5 a1.5 1.5 0 0 0 -1.5 1.5 v0.6 C7.7 5.4 6 7.7 6 10.5 v4 l-2 2.5 v1 h16 v-1 l-2 -2.5 v-4 c0 -2.8 -1.7 -5.1 -4.5 -5.9 V4 a1.5 1.5 0 0 0 -1.5 -1.5 z M10 19.5 a2 2 0 0 0 4 0 z";

    public static string Key(MapLayerKind kind) => kind switch
    {
        MapLayerKind.Manual => "manual",
        MapLayerKind.Explorer => "explorer",
        MapLayerKind.Monitoring => "monitoring",
        _ => "excel"
    };

    /// <summary>Marker outline: circle (Excel), pin (my points), diamond (Explorer), rounded square (Monitoring).</summary>
    public static string Shape(MapLayerKind kind) => kind switch
    {
        MapLayerKind.Manual => "pin",
        MapLayerKind.Explorer => "diamond",
        MapLayerKind.Monitoring => "square",
        _ => "circle"
    };

    /// <summary>A colour the user cannot change (null = the layer's own colour).</summary>
    public static string? FixedColor(MapLayerKind kind) => kind switch
    {
        MapLayerKind.Explorer => ExplorerColor,
        MapLayerKind.Monitoring => MonitoringColor,
        _ => null
    };

    /// <summary>A glyph the user cannot change (null = the layer's own icon).</summary>
    public static string? FixedGlyph(MapLayerKind kind) => kind switch
    {
        MapLayerKind.Explorer => SearchGlyph,
        MapLayerKind.Monitoring => BellGlyph,
        _ => null
    };
}
