using FastDbExplorer.Infrastructure;
using Xunit;

namespace FastDbExplorer.Tests;

public sealed class CredentialVaultTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fdbx-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "credentials.json");

    [Fact]
    public async Task Saved_password_round_trips_and_is_not_plain_text_on_disk()
    {
        if (!OperatingSystem.IsWindows()) return; // DPAPI is Windows-only
        var vault = new DpapiCredentialVault(FilePath);
        var id = Guid.NewGuid();

        await vault.SavePasswordAsync(id, "S3cret!pw");

        Assert.Equal("S3cret!pw", await vault.GetPasswordAsync(id));
        Assert.DoesNotContain("S3cret!pw", await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task Profiles_are_isolated_and_remove_deletes_only_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var vault = new DpapiCredentialVault(FilePath);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await vault.SavePasswordAsync(a, "pw-a");
        await vault.SavePasswordAsync(b, "pw-b");

        await vault.RemoveAsync(a);

        Assert.Null(await vault.GetPasswordAsync(a));
        Assert.Equal("pw-b", await vault.GetPasswordAsync(b));
    }

    [Fact]
    public async Task Unknown_profile_and_missing_file_return_null()
    {
        var vault = new DpapiCredentialVault(FilePath);
        Assert.Null(await vault.GetPasswordAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Damaged_file_is_treated_as_empty_and_can_be_rewritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath, "{ not json");
        var vault = new DpapiCredentialVault(FilePath);
        var id = Guid.NewGuid();

        Assert.Null(await vault.GetPasswordAsync(id));
        await vault.SavePasswordAsync(id, "pw");

        Assert.Equal("pw", await vault.GetPasswordAsync(id));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
