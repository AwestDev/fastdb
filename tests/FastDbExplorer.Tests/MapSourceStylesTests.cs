using FastDbExplorer.Domain;
using Xunit;

namespace FastDbExplorer.Tests;

public class MapSourceStylesTests
{
    private static readonly MapLayerKind[] AllKinds = Enum.GetValues<MapLayerKind>();

    [Fact]
    public void Each_source_has_its_own_marker_shape()
    {
        var shapes = AllKinds.Select(MapSourceStyles.Shape).ToList();

        Assert.Equal(["circle", "pin", "diamond", "square"], shapes);
        Assert.Equal(shapes.Count, shapes.Distinct().Count()); // never two sources with the same shape
    }

    [Fact]
    public void Source_keys_are_unique()
    {
        var keys = AllKinds.Select(MapSourceStyles.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Explorer_and_monitoring_results_differ_in_colour_shape_and_glyph()
    {
        var explorer = MapLayerKind.Explorer;
        var monitoring = MapLayerKind.Monitoring;

        Assert.True(MapMarkerCatalog.IsValidColor(MapSourceStyles.FixedColor(explorer)));
        Assert.True(MapMarkerCatalog.IsValidColor(MapSourceStyles.FixedColor(monitoring)));
        Assert.NotEqual(MapSourceStyles.FixedColor(explorer), MapSourceStyles.FixedColor(monitoring));
        Assert.NotEqual(MapSourceStyles.Shape(explorer), MapSourceStyles.Shape(monitoring));
        Assert.NotEqual(MapSourceStyles.FixedGlyph(explorer), MapSourceStyles.FixedGlyph(monitoring));
    }

    [Fact]
    public void Fixed_glyphs_are_plain_svg_path_data()
    {
        foreach (var kind in new[] { MapLayerKind.Explorer, MapLayerKind.Monitoring })
        {
            var glyph = MapSourceStyles.FixedGlyph(kind);
            Assert.NotNull(glyph);
            Assert.Matches(@"^M[MmLlHhVvCcSsAaZz0-9eE.,\s+\-]+$", glyph!);
            Assert.EndsWith("z", glyph.TrimEnd());
        }
    }

    [Fact]
    public void Excel_and_manual_layers_keep_their_own_colour_and_glyph()
    {
        Assert.Null(MapSourceStyles.FixedColor(MapLayerKind.Excel));
        Assert.Null(MapSourceStyles.FixedGlyph(MapLayerKind.Excel));
        Assert.Null(MapSourceStyles.FixedColor(MapLayerKind.Manual));
        Assert.Null(MapSourceStyles.FixedGlyph(MapLayerKind.Manual));
        Assert.True(MapMarkerCatalog.IsValidColor(MapSourceStyles.ManualColor));
    }

    [Fact]
    public void The_dot_glyph_used_by_pin_and_diamond_markers_exists_in_the_icon_catalog()
        => Assert.NotNull(MapMarkerCatalog.Find("dot"));
}
