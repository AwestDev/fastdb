using System.Text.Json;
using FastDbExplorer.Application.Abstractions;

namespace FastDbExplorer.Infrastructure.Maps;

/// <summary>
/// Stores the last opened map path in %AppData%\FastDbExplorer\map-settings.json.
/// Writes go to a temp file first and are then moved over the target, so a crash cannot leave a half-written file.
/// A damaged file is treated as "nothing saved" (never an error at start-up).
/// </summary>
public sealed class JsonMapSettingsStore : IMapSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonMapSettingsStore(string? filePath = null)
        => _path = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDbExplorer", "map-settings.json");

    public async Task<string?> LoadLastMapPathAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path)) return null;
            await using var stream = File.OpenRead(_path);
            var file = await JsonSerializer.DeserializeAsync<SettingsFile>(stream, Options, ct);
            var saved = file?.LastMapPath;
            return string.IsNullOrWhiteSpace(saved) ? null : saved;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveLastMapPathAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await _gate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = _path + ".tmp";
            try
            {
                await using (var stream = File.Create(temp))
                    await JsonSerializer.SerializeAsync(stream, new SettingsFile(1, path), Options, ct);
                File.Move(temp, _path, overwrite: true);
            }
            catch
            {
                if (File.Exists(temp)) File.Delete(temp);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal sealed record SettingsFile(int Version, string? LastMapPath);
}
