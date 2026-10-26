using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastDbExplorer.Application.Abstractions;

namespace FastDbExplorer.Infrastructure;

/// <summary>
/// Opt-in SQL password storage. Each password is encrypted with Windows DPAPI (current-user scope) plus an
/// app-specific entropy value and written as Base64 to %AppData%\FastDbExplorer\credentials.json.
/// The file is useless on another PC or under another Windows account (decryption fails and the user is asked again).
/// Passwords are never written to connections.json.
/// </summary>
public sealed class DpapiCredentialVault : ICredentialVault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FastDbExplorer.SqlCredentials.v1");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DpapiCredentialVault(string? path = null)
        => _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDbExplorer", "credentials.json");

    public async Task<string?> GetPasswordAsync(Guid profileId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            if (!items.TryGetValue(Key(profileId), out var blob)) return null;

            byte[]? plain = null;
            try
            {
                plain = Unprotect(Convert.FromBase64String(blob));
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or PlatformNotSupportedException)
            {
                return null; // copied from another PC/account or damaged: behave as "not saved"
            }
            finally
            {
                if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task SavePasswordAsync(Guid profileId, string password, CancellationToken ct = default)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        try
        {
            var blob = Convert.ToBase64String(Protect(plain));
            await _gate.WaitAsync(ct);
            try
            {
                var items = await ReadAsync(ct);
                items[Key(profileId)] = blob;
                await WriteAsync(items, ct);
            }
            finally { _gate.Release(); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public async Task RemoveAsync(Guid profileId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadAsync(ct);
            if (items.Remove(Key(profileId))) await WriteAsync(items, ct);
        }
        finally { _gate.Release(); }
    }

    private static string Key(Guid id) => id.ToString("D");

    private static byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Password storage needs Windows DPAPI.");
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Password storage needs Windows DPAPI.");
        return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    private async Task<Dictionary<string, string>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, Options, ct) ?? new();
        }
        catch (JsonException)
        {
            return new(); // a damaged file must never block the app; it is rewritten on the next save
        }
    }

    private async Task WriteAsync(Dictionary<string, string> items, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, items, Options, ct);
        File.Move(temp, _path, overwrite: true);
    }
}
