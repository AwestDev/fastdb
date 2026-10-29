using System.Data;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure;

public enum SpatialMode { LatLonColumns, GeographyColumn }

/// <summary>Which columns hold the coordinates of a table. Latitude/Longitude are used only with <see cref="SpatialMode.LatLonColumns"/>.</summary>
public sealed record SpatialTarget(SpatialMode Mode, string? LatColumn, string? LonColumn, string? GeographyColumn);

/// <summary>
/// Finds coordinate columns and builds a "points within radius, nearest first" query.
/// Coordinates are detected by type and by name fragments (lat/lon/lng/long/x/y), so the caller
/// can show the guess and let the user change it. Everything user-supplied is a typed parameter.
/// </summary>
public static class SpatialSearch
{
    public const double EarthRadiusMeters = 6_371_000.0;
    public const double MaxRadiusMeters = 500_000.0;
    public const string DistanceAlias = "__DistanceMeters";

    private static readonly HashSet<string> NumericTypes = ["float", "real", "decimal", "numeric"];

    public static IReadOnlyList<ColumnInfo> LatitudeCandidates(IReadOnlyList<ColumnInfo> columns) =>
        columns.Where(c => NumericTypes.Contains(c.TypeName) && IsLatitudeName(c.Name)).ToList();

    public static IReadOnlyList<ColumnInfo> LongitudeCandidates(IReadOnlyList<ColumnInfo> columns) =>
        columns.Where(c => NumericTypes.Contains(c.TypeName) && IsLongitudeName(c.Name)).ToList();

    /// <summary>Prefers a native geography column; otherwise the first numeric lat/lon pair. Null when nothing fits.</summary>
    public static SpatialTarget? Detect(IReadOnlyList<ColumnInfo> columns)
    {
        var geography = columns.FirstOrDefault(c => c.TypeName == "geography");
        if (geography is not null)
            return new SpatialTarget(SpatialMode.GeographyColumn, null, null, geography.Name);

        var lat = LatitudeCandidates(columns).FirstOrDefault();
        var lon = LongitudeCandidates(columns).FirstOrDefault();
        return lat is null || lon is null
            ? null
            : new SpatialTarget(SpatialMode.LatLonColumns, lat.Name, lon.Name, null);
    }

    public static BuiltQuery BuildNearby(
        string schema, string table, IReadOnlyList<ColumnInfo> columns, IReadOnlyList<string> selectColumns,
        SpatialTarget target, double centerLatitude, double centerLongitude, double radiusMeters, int maxRows)
    {
        if (double.IsNaN(centerLatitude) || centerLatitude < -90 || centerLatitude > 90)
            throw new ArgumentOutOfRangeException(nameof(centerLatitude), "Latitude must be between -90 and 90.");
        if (double.IsNaN(centerLongitude) || centerLongitude < -180 || centerLongitude > 180)
            throw new ArgumentOutOfRangeException(nameof(centerLongitude), "Longitude must be between -180 and 180.");
        if (double.IsNaN(radiusMeters) || radiusMeters <= 0 || radiusMeters > MaxRadiusMeters)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), "Radius must be more than 0 and at most 500 km.");
        if (selectColumns.Count == 0) throw new ArgumentException("Select at least one column.");

        var byName = columns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        string Col(string name) =>
            SelectQueryBuilder.Quote(byName.TryGetValue(name, out var c) ? c.Name : throw new ArgumentException($"Unknown column '{name}'."));

        var parameters = new List<QueryParameter>
        {
            new("@lat", SqlDbType.Float, centerLatitude),
            new("@lon", SqlDbType.Float, centerLongitude),
            new("@radius", SqlDbType.Float, radiusMeters),
            new("@take", SqlDbType.Int, Math.Clamp(maxRows, 1, SelectQueryBuilder.MaxPageSize)),
        };

        string distance;
        string present;
        switch (target.Mode)
        {
            case SpatialMode.GeographyColumn:
                var geo = Col(target.GeographyColumn ?? throw new ArgumentException("Geography column is missing."));
                distance = $"geography::Point(@lat, @lon, 4326).STDistance({geo})";
                present = $"{geo} IS NOT NULL";
                break;
            case SpatialMode.LatLonColumns:
                var lat = Col(target.LatColumn ?? throw new ArgumentException("Latitude column is missing."));
                var lon = Col(target.LonColumn ?? throw new ArgumentException("Longitude column is missing."));
                // Haversine great-circle distance in metres, computed by SQL Server.
                distance = $"2 * {EarthRadiusMeters.ToString(System.Globalization.CultureInfo.InvariantCulture)} * ASIN(SQRT("
                         + $"POWER(SIN(RADIANS({lat} - @lat) / 2), 2) + COS(RADIANS(@lat)) * COS(RADIANS({lat})) "
                         + $"* POWER(SIN(RADIANS({lon} - @lon) / 2), 2)))";
                present = $"{lat} IS NOT NULL AND {lon} IS NOT NULL";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        var select = selectColumns.Select(Col).ToList();
        select.Add($"{distance} AS {SelectQueryBuilder.Quote(DistanceAlias)}");
        var from = $"{SelectQueryBuilder.Quote(schema)}.{SelectQueryBuilder.Quote(table)}";
        var where = $"{present} AND {distance} <= @radius";

        var sql = $"SELECT TOP (@take) {string.Join(", ", select)} FROM {from} WHERE {where} ORDER BY {distance} ASC";
        return new BuiltQuery(sql, parameters);
    }

    private static bool IsLatitudeName(string name)
    {
        var n = name.ToLowerInvariant();
        return n is "y" || n.Contains("lat");
    }

    private static bool IsLongitudeName(string name)
    {
        var n = name.ToLowerInvariant();
        return n is "x" || n.Contains("lon") || n.Contains("lng");
    }
}
