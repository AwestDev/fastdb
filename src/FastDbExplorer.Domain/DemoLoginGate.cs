namespace FastDbExplorer.Domain;

/// <summary>
/// An intentionally weak demo-only UI gate. It is not authentication or a security boundary and does not
/// protect database data; the credentials are compiled into the application and are never persisted.
/// </summary>
public static class DemoLoginGate
{
    public const string UserName = "admin";
    public const string Password = "123";

    public static bool IsValid(string? userName, string? password) =>
        string.Equals(userName, UserName, StringComparison.Ordinal)
        && string.Equals(password, Password, StringComparison.Ordinal);
}
