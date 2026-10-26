using System.Text.Json;
using System.Text.Json.Serialization;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure.Monitoring;

internal sealed record MonitorFile(int Version, List<MonitorDefinition> Monitors);

/// <summary>%AppData%\FastDbExplorer\monitors.json. Definitions + last-check state only; never passwords.</summary>
public sealed class JsonMonitorStore : IMonitorStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonMonitorStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDbExplorer", "monitors.json");
    }

    public async Task<IReadOnlyList<MonitorDefinition>> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path)) return [];
            try
            {
                await using var stream = File.OpenRead(_path);
                var file = await JsonSerializer.DeserializeAsync<MonitorFile>(stream, Options, ct);
                return file?.Monitors ?? [];
            }
            catch (JsonException)
            {
                // A damaged file must not block the app: keep a copy for inspection and start empty.
                try { File.Move(_path, _path + ".bad", true); } catch (IOException) { }
                return [];
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyList<MonitorDefinition> monitors, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, new MonitorFile(1, monitors.ToList()), Options, ct);
            File.Move(temp, _path, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
