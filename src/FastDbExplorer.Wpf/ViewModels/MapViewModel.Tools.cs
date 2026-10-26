using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>
/// Map tools on the host side: the persistent "my points" layer (points the user adds by typing or picking coordinates)
/// and the actions on the temporary Explorer/Monitoring result sets. The tools themselves (go to, pick, measure) run in the
/// map page; they only report to the host when something must be saved or removed.
/// </summary>
public sealed partial class MapViewModel
{
    /// <summary>Fixed id of the one "my points" layer, so it is found again after a restart.</summary>
    public static readonly Guid ManualLayerId = new("6d616e75-616c-4c61-7965-722d6d79706f");

    /// <summary>Zooms the map to one result set (null = the newest one).</summary>
    public void ZoomToQueryResult(Guid? layerId = null)
    {
        var layer = layerId is { } wanted
            ? _queryResults.FirstOrDefault(set => set.Layer.Id == wanted)?.Layer
            : QueryResultLayer;
        if (layer is not null) CoordinateLayerZoomRequested?.Invoke(layer.Id);
    }

    /// <summary>Adds a point to the "my points" layer (created on first use) and saves it.</summary>
    public async Task AddManualPointAsync(string? name, double latitude, double longitude)
    {
        if (!CoordinateRules.IsValidLatitude(latitude) || !CoordinateRules.IsValidLongitude(longitude)) return;

        var existing = CoordinateLayers.FirstOrDefault(item => item.Id == ManualLayerId);
        var points = existing?.Layer.Points.ToList() ?? [];

        var label = string.IsNullOrWhiteSpace(name) ? MapToolStrings.ManualPointName(points.Count + 1) : name.Trim();
        if (label.Length > CoordinateStrings.MaxNameLength) label = label[..CoordinateStrings.MaxNameLength];
        points.Add(new MapPoint(Guid.NewGuid(), label, latitude, longitude, ManualLayerId));

        var layer = new MapLayer(
            ManualLayerId,
            existing?.Layer.Name ?? MapToolStrings.ManualLayerName,
            "manual",
            points,
            visibility: true, // a point added to a hidden layer would look like nothing happened
            existing?.Layer.RadiusMeters ?? 0)
        {
            IconKey = existing?.Layer.IconKey ?? "dot",
            Color = existing?.Layer.Color ?? MapSourceStyles.ManualColor
        };

        if (existing is null)
        {
            AddCoordinateLayer(layer);
        }
        else
        {
            existing.Sync(layer);
            CoordinateLayerChanged?.Invoke(layer);
        }

        SetCoordinateStatus(MapToolStrings.ManualPointAdded(label), false);
        await SaveLayerAsync(layer); // a save problem replaces the success text
    }

    /// <summary>Removes one point from "my points"; the layer disappears with its last point.</summary>
    public async Task RemoveManualPointAsync(Guid pointId)
    {
        var existing = CoordinateLayers.FirstOrDefault(item => item.Id == ManualLayerId);
        if (existing is null || existing.Layer.Points.All(p => p.Id != pointId)) return;

        var points = existing.Layer.Points.Where(p => p.Id != pointId).ToList();
        if (points.Count == 0)
        {
            await RemoveCoordinateLayerAsync(existing);
            SetCoordinateStatus(MapToolStrings.ManualPointRemoved, false);
            return;
        }

        var layer = new MapLayer(
            ManualLayerId, existing.Layer.Name, existing.Layer.FileName, points,
            existing.Layer.Visibility, existing.Layer.RadiusMeters)
        {
            IconKey = existing.Layer.IconKey,
            Color = existing.Layer.Color
        };

        existing.Sync(layer);
        CoordinateLayerChanged?.Invoke(layer);
        SetCoordinateStatus(MapToolStrings.ManualPointRemoved, false);
        await SaveLayerAsync(layer);
    }
}
