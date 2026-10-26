using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure;
using Microsoft.Data.SqlClient;

namespace FastDbExplorer.Tests;

public class ReadOnlySqlGuardTests
{
    [Theory]
    [InlineData("SELECT name FROM sys.databases")]
    [InlineData("  select 1;  ")]
    [InlineData("WITH x AS (SELECT 1 AS a) SELECT a FROM x")]
    public void Allows_read_statements(string sql) => ReadOnlySqlGuard.EnsureReadOnly(sql);

    [Theory]
    [InlineData("")]
    [InlineData("DELETE FROM Orders")]
    [InlineData("UPDATE T SET a = 1")]
    [InlineData("INSERT INTO T VALUES (1)")]
    [InlineData("DROP TABLE T")]
    [InlineData("EXEC sp_who")]
    [InlineData("SELECT 1; DROP TABLE T")]
    [InlineData("SELECT * INTO Copy FROM T")]
    public void Rejects_everything_else(string sql)
        => Assert.Throws<InvalidOperationException>(() => ReadOnlySqlGuard.EnsureReadOnly(sql));
}

public class SqlConnectionStringFactoryTests
{
    [Fact]
    public void Windows_auth_is_read_only_intent()
    {
        var s = new ConnectionSettings(@".\SQLEXPRESS", AuthenticationMode.Windows, null, null, true);
        var b = new SqlConnectionStringBuilder(SqlConnectionStringFactory.Create(s));

        Assert.True(b.IntegratedSecurity);
        Assert.Equal(ApplicationIntent.ReadOnly, b.ApplicationIntent);
        Assert.Equal("master", b.InitialCatalog);
    }

    [Fact]
    public void Sql_auth_uses_credentials_and_selected_database()
    {
        var s = new ConnectionSettings("srv", AuthenticationMode.SqlServer, "reader", "pw", false);
        var b = new SqlConnectionStringBuilder(SqlConnectionStringFactory.Create(s, "Sales"));

        Assert.Equal("reader", b.UserID);
        Assert.Equal("Sales", b.InitialCatalog);
        Assert.False(b.TrustServerCertificate);
    }

    [Fact]
    public void Empty_server_is_rejected()
        => Assert.Throws<ArgumentException>(() =>
            SqlConnectionStringFactory.Create(new ConnectionSettings(" ", AuthenticationMode.Windows, null, null, true)));
}

public class JsonConnectionProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fde-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "connections.json");

    // The only allowed mention of "password" in the profile file is the opt-in flag NAME (never a password value).
    private static string WithoutRememberFlag(string json) => json.Replace("RememberPassword", "", StringComparison.Ordinal);

    [Fact]
    public void Saved_connection_keeps_the_legacy_constructor_and_deconstruction_shape()
    {
        var profile = new SavedConnection("srv", AuthenticationMode.Windows, null);
        var (server, authentication, userName) = profile;

        Assert.Equal("srv", server);
        Assert.Equal(AuthenticationMode.Windows, authentication);
        Assert.Null(userName);
    }

    [Fact]
    public async Task Saves_without_duplicates_and_never_writes_a_password()
    {
        var store = new JsonConnectionProfileStore(FilePath);
        var c = new SavedConnection("SRV1", AuthenticationMode.SqlServer, "reader");

        await store.SaveAsync(c);
        await store.SaveAsync(c with { Server = "srv1" });

        Assert.Single(await store.LoadAsync());
        Assert.DoesNotContain("assword", WithoutRememberFlag(await File.ReadAllTextAsync(FilePath)));
    }

    [Fact]
    public async Task Remember_password_flag_defaults_to_off_and_round_trips()
    {
        var store = new JsonConnectionProfileStore(FilePath);
        var plain = new SavedConnection("srv", AuthenticationMode.SqlServer, "reader", "A", Id: Guid.NewGuid());
        var remembered = new SavedConnection("srv", AuthenticationMode.SqlServer, "reader2", "B", Id: Guid.NewGuid(), RememberPassword: true);

        await store.SaveAsync(plain);
        await store.SaveAsync(remembered);

        var profiles = await store.LoadAsync();
        Assert.False(profiles.Single(p => p.Id == plain.Id).RememberPassword);
        Assert.True(profiles.Single(p => p.Id == remembered.Id).RememberPassword);
    }

    [Fact]
    public async Task Corrupt_file_loads_as_empty()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath, "{ not json");
        Assert.Empty(await new JsonConnectionProfileStore(FilePath).LoadAsync());
    }

    [Fact]
    public async Task Migrates_legacy_recent_connection_to_named_profile_without_password()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath,
            "[{\"Server\":\"srv\",\"Authentication\":\"SqlServer\",\"UserName\":\"reader\"}]");

        var store = new JsonConnectionProfileStore(FilePath);
        var first = Assert.Single(await store.LoadAsync());
        var savedJson = await File.ReadAllTextAsync(FilePath);
        var second = Assert.Single(await store.LoadAsync());

        Assert.Equal("srv (reader)", first.Name);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.True(first.TrustServerCertificate);
        Assert.False(first.RememberPassword); // legacy profiles never opt in by themselves
        Assert.False(WithoutRememberFlag(savedJson).Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Different_profile_ids_allow_same_server_and_rename_in_place()
    {
        var store = new JsonConnectionProfileStore(FilePath);
        var first = new SavedConnection("srv", AuthenticationMode.Windows, null, "Production", Id: Guid.NewGuid());
        var second = new SavedConnection("srv", AuthenticationMode.Windows, null, "Reporting", Id: Guid.NewGuid());

        await store.SaveAsync(first);
        await store.SaveAsync(second);
        await store.SaveAsync(first with { Name = "Production RO" });

        var profiles = await store.LoadAsync();
        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, profile => profile.Id == first.Id && profile.Name == "Production RO");
        Assert.Contains(profiles, profile => profile.Id == second.Id && profile.Name == "Reporting");
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}

public class DemoLoginGateTests
{
    [Fact]
    public void Accepts_only_the_documented_demo_credentials()
    {
        Assert.True(DemoLoginGate.IsValid("admin", "123"));
        Assert.False(DemoLoginGate.IsValid("admin", "wrong"));
        Assert.False(DemoLoginGate.IsValid("Admin", "123"));
    }
}
