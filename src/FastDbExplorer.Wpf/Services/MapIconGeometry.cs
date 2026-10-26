using System.Windows.Media;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Services;

/// <summary>WPF-side cache: turns the catalog's path data and colours into frozen Geometry / Brush objects (UI thread only).</summary>
public static class MapIconGeometry
{
    private static readonly Dictionary<string, Geometry> ShapeCache = [];
    private static readonly Dictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The glyph for an icon key; null for "no icon" or an unknown key.</summary>
    public static Geometry? For(string? key)
    {
        if (key is null) return null;
        if (ShapeCache.TryGetValue(key, out var cached)) return cached;

        var icon = MapMarkerCatalog.Find(key);
        if (icon is null) return null;

        try
        {
            var shape = Geometry.Parse(icon.PathData);
            shape.Freeze();
            ShapeCache[key] = shape;
            return shape;
        }
        catch (FormatException)
        {
            return null; // a malformed glyph must never break the layer list
        }
    }

    public static Brush BrushFor(string hex)
    {
        if (BrushCache.TryGetValue(hex, out var cached)) return cached;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        BrushCache[hex] = brush;
        return brush;
    }
}
