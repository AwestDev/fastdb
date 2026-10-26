using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure.CoordinateLayers;
using Xunit;

namespace FastDbExplorer.Tests;

public sealed class MapMarkerCatalogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fdbx-markers-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Icon_keys_are_unique_and_every_path_is_plain_svg_path_data()
    {
        Assert.Equal(MapMarkerCatalog.Icons.Count, MapMarkerCatalog.Icons.Select(i => i.Key).Distinct().Count());
        Assert.NotEmpty(MapMarkerCatalog.Icons);

        foreach (var icon in MapMarkerCatalog.Icons)
        {
            Assert.Matches(@"^M[MmLlHhVvCcSsAaZz0-9eE.,\s+\-]+$", icon.PathData); // nothing but path commands and numbers
            Assert.EndsWith("z", icon.PathData.TrimEnd());
        }
    }

    [Fact]
    public void Palette_colours_are_valid_and_unique()
    {
        Assert.All(MapMarkerCatalog.Palette, c => Assert.True(MapMarkerCatalog.IsValidColor(c), c));
        Assert.Equal(MapMarkerCatalog.Palette.Count, MapMarkerCatalog.Palette.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#12345G")]
    [InlineData("url(javascript:alert(1))")]
    public void Invalid_colours_are_rejected(string? value) => Assert.False(MapMarkerCatalog.IsValidColor(value));

    [Fact]
    public void Effective_colour_is_the_chosen_one_or_a_stable_palette_colour()
    {
        var id = Guid.NewGuid();

        Assert.Equal("#DC2626", MapMarkerCatalog.EffectiveColor(id, "#dc2626"));

        var automatic = MapMarkerCatalog.EffectiveColor(id, null);
        Assert.Contains(automatic, MapMarkerCatalog.Palette);
        Assert.Equal(automatic, MapMarkerCatalog.EffectiveColor(id, "not-a-colour"));
        Assert.Equal(automatic, MapMarkerCatalog.EffectiveColor(id, null)); // same id, same colour every time
    }

    [Fact]
    public async Task Marker_style_round_trips_through_the_store()
    {
        var store = new JsonCoordinateLayerStore(_dir);
        var layer = NewLayer();
        layer.IconKey = "hospital";
        layer.Color = "#16A34A";

        await store.SaveAsync(layer);

        var loaded = Assert.Single(await store.LoadAllAsync());
        Assert.Equal("hospital", loaded.IconKey);
        Assert.Equal("#16A34A", loaded.Color);
    }

    [Fact]
    public async Task A_layer_without_style_loads_with_the_default_marker()
    {
        var store = new JsonCoordinateLayerStore(_dir);
        await store.SaveAsync(NewLayer());

        var loaded = Assert.Single(await store.LoadAllAsync());
        Assert.Null(loaded.IconKey);
        Assert.Null(loaded.Color);
    }

    [Fact]
    public async Task Unknown_icon_and_invalid_colour_are_dropped_not_fatal()
    {
        var store = new JsonCoordinateLayerStore(_dir);
        var layer = NewLayer();
        layer.IconKey = "does-not-exist";
        layer.Color = "javascript:alert(1)";
        await store.SaveAsync(layer);

        var loaded = Assert.Single(await store.LoadAllAsync()); // the layer itself still loads
        Assert.Null(loaded.IconKey);
        Assert.Null(loaded.Color);
    }

    private static MapLayer NewLayer()
    {
        var id = Guid.NewGuid();
        return new MapLayer(id, "Test layer", "points.xlsx", [new MapPoint(Guid.NewGuid(), "P1", 35.7, 51.4, id)]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
