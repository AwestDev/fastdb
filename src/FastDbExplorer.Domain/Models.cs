using System.Text.Json.Serialization;

namespace FastDbExplorer.Domain;

public enum AuthenticationMode { Windows, SqlServer }

/// <summary>Everything needed to open a connection. The password lives in memory only.</summary>
public sealed record ConnectionSettings(
    string Server,
    AuthenticationMode Authentication,
    string? UserName,
    string? Password,
    bool TrustServerCertificate);

/// <summary>
/// A remembered connection. It has no password field: <see cref="RememberPassword"/> only says that an encrypted
/// SQL password is stored for this profile in the credential vault (ICredentialVault). The password itself is never
/// part of this record, never written to connections.json and never shown in the UI.
/// </summary>
public sealed record SavedConnection
{
    public SavedConnection() { }

    public SavedConnection(
        string Server,
        AuthenticationMode Authentication,
        string? UserName,
        string Name = "",
        bool TrustServerCertificate = true,
        Guid Id = default,
        bool RememberPassword = false)
    {
        this.Server = Server;
        this.Authentication = Authentication;
        this.UserName = UserName;
        this.Name = Name;
        this.TrustServerCertificate = TrustServerCertificate;
        this.Id = Id;
        this.RememberPassword = RememberPassword;
    }

    public string Server { get; init; } = "";
    public AuthenticationMode Authentication { get; init; }
    public string? UserName { get; init; }
    public string Name { get; init; } = "";
    public bool TrustServerCertificate { get; init; } = true;
    public Guid Id { get; init; }
    public bool RememberPassword { get; init; }

    public void Deconstruct(out string Server, out AuthenticationMode Authentication, out string? UserName)
    {
        Server = this.Server;
        Authentication = this.Authentication;
        UserName = this.UserName;
    }

    [JsonIgnore]
    public string Description => Authentication == AuthenticationMode.Windows ? "Windows" : $"SQL · {UserName}";
}

public sealed record DatabaseInfo(string Name);

public sealed record TableInfo(string Schema, string Name, long ApproxRows)
{
    public string FullName => $"{Schema}.{Name}";
}

public enum DatabaseErrorKind
{
    LoginFailed,
    ServerUnreachable,
    Timeout,
    PagingNotSupported,
    PagingDetectionFailed,
    PagingFallbackFailed,
    Other
}

public sealed class DatabaseAccessException(DatabaseErrorKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public DatabaseErrorKind Kind { get; } = kind;
}
