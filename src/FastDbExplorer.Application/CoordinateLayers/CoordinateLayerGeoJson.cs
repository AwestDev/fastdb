using System.Text.Json.Nodes;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Application.CoordinateLayers;

/// <summary>
/// Turns a <see cref="MapLayer"/> into the GeoJSON the map page draws. Pure data, no UI types, so it is unit-tested.
/// Coordinates are WGS84 [longitude, latitude] (GeoJSON order). Radius circles are real polygons built in
/// <see cref="GeoMath.CircleRing"/>; the page never converts metres itself.
/// </summary>
public static class CoordinateLayerGeoJson
{
    private const int MaxDetailFields = 8;
    private const int MaxDetailKeyLength = 80;
    private const int MaxDetailValueLength = 240;
    private const int MaxDetailTextLength = 1_200;

    /// <summary>One Point feature per layer point, with its name (for the label).</summary>
    public static JsonObject Points(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var features = new JsonArray();
        foreach (var point in layer.Points)
        {
            var properties = new JsonObject { ["id"] = point.Id.ToString(), ["name"] = point.Name };
            if (LimitedDetails(point.Details) is { Count: > 0 } details)
            {
                var detailObject = new JsonObject();
                foreach (var (key, value) in details)
                    detailObject[key] = value;
                properties["details"] = detailObject;
            }

            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "Point",
                    ["coordinates"] = new JsonArray(point.Longitude, point.Latitude)
                },
                ["properties"] = properties
            });
        }
        return Collection(features);
    }

    /// <summary>One Polygon feature per point when the layer radius is above 0; an empty collection otherwise.</summary>
    public static JsonObject Circles(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var features = new JsonArray();
        if (layer.RadiusMeters > 0)
        {
            foreach (var point in layer.Points)
            {
                var ring = new JsonArray();
                foreach (var (lng, lat) in GeoMath.CircleRing(point.Latitude, point.Longitude, layer.RadiusMeters))
                    ring.Add(new JsonArray(lng, lat));

                features.Add(new JsonObject
                {
                    ["type"] = "Feature",
                    ["geometry"] = new JsonObject
                    {
                        ["type"] = "Polygon",
                        ["coordinates"] = new JsonArray(ring)
                    },
                    ["properties"] = new JsonObject { ["id"] = point.Id.ToString(), ["name"] = point.Name }
                });
            }
        }
        return Collection(features);
    }

    /// <summary>[west, south, east, north] around all points and their circles; null for a layer without points.</summary>
    public static JsonArray? Bounds(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return GeoMath.LayerBounds(layer.Points, layer.RadiusMeters) is { } b
            ? new JsonArray(b.West, b.South, b.East, b.North)
            : null;
    }

    private static JsonObject Collection(JsonArray features) =>
        new() { ["type"] = "FeatureCollection", ["features"] = features };

    private static IReadOnlyDictionary<string, string>? LimitedDetails(IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0) return null;

        var limited = new Dictionary<string, string>(StringComparer.Ordinal);
        var textLength = 0;
        foreach (var (rawKey, rawValue) in details)
        {
            if (limited.Count == MaxDetailFields || textLength >= MaxDetailTextLength) break;
            if (string.IsNullOrWhiteSpace(rawKey)) continue;

            var key = Truncate(rawKey, MaxDetailKeyLength);
            var remaining = MaxDetailTextLength - textLength - key.Length;
            if (remaining <= 0) break;
            var value = Truncate(rawValue ?? "", Math.Min(MaxDetailValueLength, remaining));
            limited[key] = value;
            textLength += key.Length + value.Length;
        }

        return limited;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        var length = Math.Max(0, maxLength - 1);
        if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            length--;
        return value[..length] + "…";
    }
}
