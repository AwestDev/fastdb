using System.Text.Json;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

/// <summary>
/// Host side of the map tools overlay (map-tools.js). The page draws the tools itself; this part sends it the texts and the
/// list of Explorer/Monitoring result sets (one chip each), tells it which kind of layer is which (so each source gets its own
/// marker shape), and carries out the few things the page cannot do alone: save/delete a "my points" point, zoom to or remove a result set.
/// </summary>
public partial class MapView
{
    private void PushToolsInit() => Post(new { type = "toolsInit", labels = MapToolStrings.Labels });

    /// <summary>Sends the current list of result sets (possibly empty); the page shows one chip per set.</summary>
    private void PostToolResults()
    {
        var items = (_viewModel?.QueryResults ?? [])
            .Select(set => new
            {
                id = set.Layer.Id.ToString(),
                title = set.Layer.Name,
                count = set.Layer.Points.Count,
                kind = MapSourceStyles.Key(set.Kind),
                color = MapSourceStyles.FixedColor(set.Kind) ?? MapSourceStyles.ExplorerColor
            })
            .ToList();
        Post(new { type = "toolResults", items });
    }

    private MapLayerKind KindOf(MapLayer layer)
    {
        if (layer.Id == MapViewModel.ManualLayerId) return MapLayerKind.Manual;
        var set = _viewModel?.QueryResults.FirstOrDefault(s => s.Layer.Id == layer.Id);
        return set?.Kind ?? MapLayerKind.Excel;
    }

    /// <summary>Handles the messages of the tools overlay; false = not a tools message.</summary>
    private bool HandleToolMessage(string? type, JsonElement root)
    {
        if (_viewModel is null) return false;
        switch (type)
        {
            case "toolAddPoint":
                if (root.TryGetProperty("lat", out var lat) && lat.TryGetDouble(out var latitude)
                    && root.TryGetProperty("lng", out var lng) && lng.TryGetDouble(out var longitude))
                    _ = _viewModel.AddManualPointAsync(Text(root, "name"), latitude, longitude);
                return true;
            case "toolDeletePoint":
                if (Guid.TryParse(Text(root, "pointId"), out var pointId)) _ = _viewModel.RemoveManualPointAsync(pointId);
                return true;
            case "toolSearchNearby":
                if (root.TryGetProperty("lat", out var searchLat) && searchLat.TryGetDouble(out var sLat)
                    && root.TryGetProperty("lng", out var searchLng) && searchLng.TryGetDouble(out var sLng))
                    _viewModel.RequestSpatialSearch(sLat, sLng);
                return true;
            case "toolZoomResult":
                _viewModel.ZoomToQueryResult(Guid.TryParse(Text(root, "id"), out var zoomId) ? zoomId : null);
                return true;
            case "toolClearResult":
                // With an id: remove that set. Without one: remove all sets.
                if (Guid.TryParse(Text(root, "id"), out var removeId)) _viewModel.RemoveQueryResult(removeId);
                else _viewModel.ClearQueryResultLayer();
                return true;
            default:
                return false;
        }
    }
}
