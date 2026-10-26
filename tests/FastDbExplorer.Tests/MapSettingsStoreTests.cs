using FastDbExplorer.Infrastructure.Maps;

namespace FastDbExplorer.Tests;

public class MapSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fde-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "map-settings.json");

    [Fact]
    public async Task Nothing_saved_loads_as_null()
        => Assert.Null(await new JsonMapSettingsStore(FilePath).LoadLastMapPathAsync());

    [Fact]
    public async Task Saved_path_round_trips_including_non_ascii()
    {
        var store = new JsonMapSettingsStore(FilePath);
        const string path = @"D:\نقشه‌ها\iran.mbtiles";

        await store.SaveLastMapPathAsync(path);

        Assert.Equal(path, await new JsonMapSettingsStore(FilePath).LoadLastMapPathAsync());
    }

    [Fact]
    public async Task Saving_again_replaces_the_previous_path()
    {
        var store = new JsonMapSettingsStore(FilePath);

        await store.SaveLastMapPathAsync(@"C:\a.mbtiles");
        await store.SaveLastMapPathAsync(@"C:\b.gmdb");

        Assert.Equal(@"C:\b.gmdb", await store.LoadLastMapPathAsync());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task Corrupt_file_loads_as_null()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath, "{ not json");

        Assert.Null(await new JsonMapSettingsStore(FilePath).LoadLastMapPathAsync());
    }

    [Fact]
    public async Task Blank_path_is_rejected()
        => await Assert.ThrowsAsync<ArgumentException>(() => new JsonMapSettingsStore(FilePath).SaveLastMapPathAsync("  "));

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
