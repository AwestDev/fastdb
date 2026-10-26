using System.Text.Json;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure.CoordinateLayers;

/// <summary>
/// One JSON file per layer in %AppData%\FastDbExplorer\coordinate-layers\{layer id}.json.
/// Writes go to a temp file first and are then moved over the target, so a crash cannot leave a half-written layer.
/// A damaged file, a file from a newer version or a file that breaks the layer rules is skipped (and left on disk),
/// so one bad file never stops the application or hides the other layers.
/// The marker style (iconKey, color) is optional: older files without it load with the default marker, and an unknown
/// icon or an invalid colour is dropped instead of rejecting the whole layer.
/// </summary>
public sealed class JsonCoordinateLayerStore : ICoordinateLayerStore
{
    internal const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonCoordinateLayerStore(string? directory = null)
        => _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDbExplorer", "coordinate-layers");

    public async Task<IReadOnlyList<MapLayer>> LoadAllAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_directory)) return [];

        var layers = new List<MapLayer>();
        await _gate.WaitAsync(ct);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                var layer = await TryReadAsync(file, ct);
                if (layer is not null) layers.Add(layer);
            }
        }
        finally
        {
            _gate.Release();
        }

        return layers
            .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Id)
            .ToList();
    }

    public async Task SaveAsync(MapLayer layer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var dto = ToDto(layer);

        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_directory);
            var target = PathFor(layer.Id);
            var temp = Path.Combine(_directory, layer.Id.ToString("N") + ".tmp");
            try
            {
                await using (var stream = File.Create(temp))
                    await JsonSerializer.SerializeAsync(stream, dto, Options, ct);
                File.Move(temp, target, overwrite: true);
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

    public async Task<bool> RemoveAsync(Guid layerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = PathFor(layerId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string PathFor(Guid id) => Path.Combine(_directory, id.ToString("N") + ".json");

    private static async Task<MapLayer?> TryReadAsync(string file, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            var dto = await JsonSerializer.DeserializeAsync<LayerFile>(stream, Options, ct);
            return dto is null ? null : ToLayer(dto);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException
                                       or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static MapLayer? ToLayer(LayerFile file)
    {
        if (file.Version != CurrentVersion || file.Points is null) return null;

        var points = new List<MapPoint>(file.Points.Count);
        foreach (var p in file.Points)
        {
            if (p?.Name is null) return null;
            points.Add(new MapPoint(p.Id, p.Name, p.Latitude, p.Longitude, file.Id));
        }

        // The MapLayer constructor re-validates everything; it throws ArgumentException for a file that breaks the rules.
        var layer = new MapLayer(file.Id, file.Name, file.FileName, points, file.Visibility, file.RadiusMeters)
        {
            IconKey = MapMarkerCatalog.Find(file.IconKey) is null ? null : file.IconKey,
            Color = MapMarkerCatalog.IsValidColor(file.Color) ? file.Color!.ToUpperInvariant() : null
        };
        return layer;
    }

    private static LayerFile ToDto(MapLayer layer) => new(
        CurrentVersion,
        layer.Id,
        layer.Name,
        layer.FileName,
        layer.Visibility,
        layer.RadiusMeters,
        layer.Points.Select(p => new PointEntry(p.Id, p.Name, p.Latitude, p.Longitude)).ToList(),
        layer.IconKey,
        layer.Color);

    // On-disk shape (version 1). Kept separate from the domain types so the file format can evolve on its own.
    // IconKey and Color were added later as optional fields: files without them still load.
    internal sealed record LayerFile(
        int Version,
        Guid Id,
        string Name,
        string FileName,
        bool Visibility,
        double RadiusMeters,
        List<PointEntry>? Points,
        string? IconKey = null,
        string? Color = null);

    internal sealed record PointEntry(Guid Id, string Name, double Latitude, double Longitude);
}
