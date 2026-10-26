namespace FastDbExplorer.Application.Abstractions;

/// <summary>
/// Optional per-profile storage for SQL passwords. Implementations must never keep a password in plain text.
/// A profile only uses the vault when its <c>RememberPassword</c> flag is on (off by default).
/// </summary>
public interface ICredentialVault
{
    /// <summary>Returns the stored password, or null when none exists or it cannot be decrypted (then the user is asked again).</summary>
    Task<string?> GetPasswordAsync(Guid profileId, CancellationToken ct = default);

    Task SavePasswordAsync(Guid profileId, string password, CancellationToken ct = default);

    Task RemoveAsync(Guid profileId, CancellationToken ct = default);
}
