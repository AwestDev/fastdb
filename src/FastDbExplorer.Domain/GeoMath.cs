using System.Globalization;
using System.Text;

namespace FastDbExplorer.Domain;

/// <summary>Axis-aligned WGS84 box in degrees. A single point without radius gives West == East and South == North.</summary>
public readonly record struct GeoBounds(double West, double South, double East, double North)
{
    public bool IsPoint => West == East && South == North;
}

/// <summary>
/// Metre-accurate geometry for layer points (WGS84 / EPSG:4326, degrees). The map page draws in Web Mercator, where a
/// fixed number of metres covers a different number of degrees (and pixels) at every latitude. So a radius is never
/// sent to the page as a number: it is turned here into a ring of real WGS84 vertices, and the map projects the ring.
/// Distances use a sphere with the mean Earth radius, the same value MapLibre's scale bar uses (error vs. the
/// WGS84 ellipsoid is below 0.5 %).
/// </summary>
public static class GeoMath
{
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>Vertices per circle (the ring has one more, it is closed). Chord error is about 0.12 % of the radius.</summary>
    public const int CircleSegments = 64;

    /// <summary>
    /// Closed counter-clockwise ring (RFC 7946) of points exactly <paramref name="radiusMeters"/> from the centre.
    /// Longitudes are not wrapped, so a circle over the antimeridian stays one connected shape (values may pass ±180).
    /// </summary>
    public static IReadOnlyList<(double Longitude, double Latitude)> CircleRing(
        double latitude, double longitude, double radiusMeters, int segments = CircleSegments)
    {
        if (!CoordinateRules.IsValidLatitude(latitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        if (!CoordinateRules.IsValidLongitude(longitude))
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        if (!CoordinateRules.IsValidRadiusMeters(radiusMeters) || radiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), radiusMeters, "The radius must be greater than 0 and within the allowed range.");
        if (segments < 8)
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "A circle needs at least 8 segments.");

        var phi1 = ToRadians(latitude);
        var lambda1 = ToRadians(longitude);
        var delta = Math.Min(radiusMeters / EarthRadiusMeters, Math.PI); // angular distance
        var sinPhi1 = Math.Sin(phi1);
        var cosPhi1 = Math.Cos(phi1);
        var sinDelta = Math.Sin(delta);
        var cosDelta = Math.Cos(delta);

        var ring = new (double Longitude, double Latitude)[segments + 1];
        for (var i = 0; i < segments; i++)
        {
            var theta = -2 * Math.PI * i / segments; // decreasing bearing = counter-clockwise
            var sinPhi2 = Math.Clamp(sinPhi1 * cosDelta + cosPhi1 * sinDelta * Math.Cos(theta), -1, 1);
            var phi2 = Math.Asin(sinPhi2);
            var lambda2 = lambda1 + Math.Atan2(Math.Sin(theta) * sinDelta * cosPhi1, cosDelta - sinPhi1 * sinPhi2);
            ring[i] = (ToDegrees(lambda2), ToDegrees(phi2));
        }
        ring[segments] = ring[0];
        return ring;
    }

    /// <summary>Box around all points, grown by their radius circles. Null when there are no points.</summary>
    public static GeoBounds? LayerBounds(IEnumerable<MapPoint> points, double radiusMeters)
    {
        ArgumentNullException.ThrowIfNull(points);
        double west = double.MaxValue, south = double.MaxValue, east = double.MinValue, north = double.MinValue;
        var any = false;

        foreach (var point in points)
        {
            any = true;
            if (radiusMeters > 0)
            {
                foreach (var (lng, lat) in CircleRing(point.Latitude, point.Longitude, radiusMeters))
                    Extend(lng, lat);
            }
            else
            {
                Extend(point.Longitude, point.Latitude);
            }
        }

        return any ? new GeoBounds(west, south, east, north) : null;

        void Extend(double lng, double lat)
        {
            west = Math.Min(west, lng);
            east = Math.Max(east, lng);
            south = Math.Min(south, lat);
            north = Math.Max(north, lat);
        }
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}

/// <summary>Parses the radius the user types (metres only). Accepts Persian/Arabic digits and the Persian decimal point.</summary>
public static class RadiusInput
{
    /// <summary>
    /// A comma is rejected on purpose: "1,500" could mean 1500 or 1.5, and a silent 1000x error on a map is worse than a message.
    /// </summary>
    public static bool TryParse(string? text, out double meters)
    {
        meters = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = new StringBuilder(text.Length);
        foreach (var ch in text.Trim())
        {
            if (ch is >= '\u06F0' and <= '\u06F9') normalized.Append((char)('0' + (ch - '\u06F0')));      // Persian digits
            else if (ch is >= '\u0660' and <= '\u0669') normalized.Append((char)('0' + (ch - '\u0660'))); // Arabic-Indic digits
            else if (ch == '\u066B') normalized.Append('.');                                               // Arabic decimal separator
            else normalized.Append(ch);
        }

        if (!double.TryParse(normalized.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !CoordinateRules.IsValidRadiusMeters(value))
            return false;

        meters = value;
        return true;
    }
}
