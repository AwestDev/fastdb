using FastDbExplorer.Domain;
using Xunit;

namespace FastDbExplorer.Tests;

public sealed class MapMarkerCategoryTests
{
    [Fact]
    public void Every_icon_belongs_to_exactly_one_category()
    {
        var all = MapMarkerCatalog.Categories.SelectMany(c => c.Icons.Select(i => i.Key)).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(MapMarkerCatalog.Icons.Count, all.Count);
        Assert.Equal(all.OrderBy(k => k), MapMarkerCatalog.Icons.Select(i => i.Key).OrderBy(k => k));
    }

    [Fact]
    public void Every_category_has_at_least_one_icon_and_the_default_category_exists()
    {
        Assert.All(MapMarkerCatalog.Categories, c => Assert.NotEmpty(c.Icons));
        Assert.Contains(MapMarkerCatalog.Categories, c => c.Key == MapMarkerCatalog.DefaultCategory);
    }

    [Fact]
    public void Category_keys_are_unique()
    {
        var keys = MapMarkerCatalog.Categories.Select(c => c.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Theory]
    [InlineData("pin")]
    [InlineData("dot")]
    [InlineData("hospital")]
    [InlineData("school")]
    [InlineData("mosque")]
    [InlineData("bolt")]
    [InlineData("drop")]
    [InlineData("warning")]
    [InlineData("shield")]
    [InlineData("camera")]
    [InlineData("tower")]
    [InlineData("plane")]
    [InlineData("ship")]
    [InlineData("bus")]
    [InlineData("shop")]
    [InlineData("person")]
    [InlineData("building")]
    [InlineData("bank")]
    [InlineData("factory")]
    [InlineData("home")]
    [InlineData("star")]
    [InlineData("heart")]
    [InlineData("flag")]
    [InlineData("target")]
    public void Keys_saved_by_earlier_versions_still_exist(string key) =>
        Assert.NotNull(MapMarkerCatalog.Find(key));

    [Fact]
    public void Every_new_icon_path_is_plain_svg_path_data_ending_with_a_close()
    {
        foreach (var icon in MapMarkerCatalog.Icons)
        {
            Assert.Matches(@"^M[MmLlHhVvCcSsAaZz0-9eE.,\s+\-]+$", icon.PathData);
            Assert.EndsWith("z", icon.PathData.TrimEnd());
        }
    }

    [Fact]
    public void The_icon_library_is_large_enough_to_be_useful()
    {
        Assert.True(MapMarkerCatalog.Icons.Count >= 50, $"only {MapMarkerCatalog.Icons.Count} icons");
    }
}
