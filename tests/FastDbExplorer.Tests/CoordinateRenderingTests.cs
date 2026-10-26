using System.Text.Json.Nodes;
using FastDbExplorer.Application.CoordinateLayers;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Tests;

public class CoordinateRenderingTests
{
    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * GeoMath.EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    private static MapLayer Layer(double radius, params (double Lat, double Lng)[] coordinates)
    {
        var id = Guid.NewGuid();
        var points = coordinates.Select((c, i) => new MapPoint(Guid.NewGuid(), $"P{i}", c.Lat, c.Lng, id));
        return new MapLayer(id, "Test", "test.xlsx", points, radiusMeters: radius);
    }

    [Theory]
    [InlineData(0, 0, 1_000)]
    [InlineData(35.7, 51.4, 2_500)]
    [InlineData(60, 10, 50_000)]
    [InlineData(-33.9, 151.2, 1_000_000)]
    public void Circle_vertices_are_exactly_the_radius_away(double lat, double lng, double radius)
    {
        foreach (var (vLng, vLat) in GeoMath.CircleRing(lat, lng, radius))
            Assert.InRange(Haversine(lat, lng, vLat, vLng), radius * 0.9999, radius * 1.0001);
    }

    [Fact]
    public void Circle_is_closed_and_counter_clockwise()
    {
        var ring = GeoMath.CircleRing(35.7, 51.4, 5_000);
        Assert.Equal(GeoMath.CircleSegments + 1, ring.Count);
        Assert.Equal(ring[0], ring[^1]);

        double area = 0;
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var (x1, y1) = (ring[i].Longitude - 51.4, ring[i].Latitude - 35.7);
            var (x2, y2) = (ring[i + 1].Longitude - 51.4, ring[i + 1].Latitude - 35.7);
            area += x1 * y2 - x2 * y1;
        }
        Assert.True(area > 0);
    }

    [Fact]
    public void A_metre_covers_more_degrees_of_longitude_at_high_latitude()
    {
        // The reason the radius is converted per point: 1 km is ~0.009 deg of latitude everywhere,
        // but ~0.018 deg of longitude at 60 deg north (1 / cos 60).
        var layer = Layer(1_000, (60, 10));
        var b = GeoMath.LayerBounds(layer.Points, layer.RadiusMeters)!.Value;
        Assert.InRange(b.North - 60, 0.0088, 0.0092);
        Assert.InRange(b.East - 10, 0.0170, 0.0190);
    }

    [Fact]
    public void Bounds_without_radius_are_the_points_and_a_single_point_is_a_point()
    {
        var two = Layer(0, (10, 20), (12, 25));
        Assert.Equal(new GeoBounds(20, 10, 25, 12), GeoMath.LayerBounds(two.Points, 0)!.Value);
        Assert.True(GeoMath.LayerBounds(Layer(0, (10, 20)).Points, 0)!.Value.IsPoint);
        Assert.Null(GeoMath.LayerBounds([], 100));
    }

    [Fact]
    public void Bounds_grow_by_the_radius()
    {
        var b = GeoMath.LayerBounds(Layer(111_195.08, (0, 0)).Points, 111_195.08)!.Value; // ~1 degree on the sphere
        Assert.InRange(b.North, 0.999, 1.001);
        Assert.InRange(b.South, -1.001, -0.999);
    }

    [Theory]
    [InlineData("1500", 1500)]
    [InlineData("  250.5 ", 250.5)]
    [InlineData("۱۵۰۰", 1500)]
    [InlineData("٢٥٠٫٥", 250.5)]
    [InlineData("0", 0)]
    public void Radius_input_accepts_numbers_in_metres(string text, double expected)
    {
        Assert.True(RadiusInput.TryParse(text, out var meters));
        Assert.Equal(expected, meters);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1,5")]
    [InlineData("1,500")]
    [InlineData("20037509")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Radius_input_rejects_bad_values(string text) => Assert.False(RadiusInput.TryParse(text, out _));

    [Fact]
    public void GeoJson_has_one_point_per_row_in_lng_lat_order()
    {
        var points = (JsonArray)CoordinateLayerGeoJson.Points(Layer(0, (35.7, 51.4), (36, 52)))["features"]!;
        Assert.Equal(2, points.Count);
        var coordinates = points[0]!["geometry"]!["coordinates"]!;
        Assert.Equal(51.4, coordinates[0]!.GetValue<double>());
        Assert.Equal(35.7, coordinates[1]!.GetValue<double>());
        Assert.Null(points[0]!["properties"]!["details"]);
    }

    [Fact]
    public void GeoJson_query_point_details_are_bounded_and_optional()
    {
        var id = Guid.NewGuid();
        var details = Enumerable.Range(0, 10)
            .ToDictionary(index => $"Field {index}", index => new string('x', index == 0 ? 400 : 50));
        var point = new MapPoint(Guid.NewGuid(), "Query row", 35.7, 51.4, id, details);
        var layer = new MapLayer(id, "Query results", "dbo.Sample", [point]);

        var properties = (JsonObject)((JsonArray)CoordinateLayerGeoJson.Points(layer)["features"]!)[0]!["properties"]!;
        var serializedDetails = (JsonObject)properties["details"]!;

        Assert.Equal("Query row", properties["name"]!.GetValue<string>());
        Assert.Equal(8, serializedDetails.Count);
        Assert.All(serializedDetails.Select(pair => pair.Value!.GetValue<string>()), value => Assert.True(value.Length <= 240));
        Assert.Equal(240, serializedDetails["Field 0"]!.GetValue<string>().Length);
        Assert.True(serializedDetails.Sum(pair => pair.Key.Length + pair.Value!.GetValue<string>().Length) <= 1_200);
    }

    [Fact]
    public void GeoJson_details_do_not_split_surrogate_pairs_when_truncated()
    {
        var id = Guid.NewGuid();
        var value = new string('a', 238) + "😀z";
        var point = new MapPoint(Guid.NewGuid(), "Query row", 35.7, 51.4, id,
            new Dictionary<string, string> { ["Long value"] = value });
        var layer = new MapLayer(id, "Query results", "dbo.Sample", [point]);

        var properties = (JsonObject)((JsonArray)CoordinateLayerGeoJson.Points(layer)["features"]!)[0]!["properties"]!;
        var serialized = ((JsonObject)properties["details"]!)["Long value"]!.GetValue<string>();

        Assert.Equal(new string('a', 238) + "…", serialized);
    }

    [Fact]
    public void GeoJson_circles_exist_only_when_the_radius_is_above_zero()
    {
        Assert.Empty((JsonArray)CoordinateLayerGeoJson.Circles(Layer(0, (35.7, 51.4)))["features"]!);

        var circles = (JsonArray)CoordinateLayerGeoJson.Circles(Layer(500, (35.7, 51.4), (36, 52)))["features"]!;
        Assert.Equal(2, circles.Count);
        var ring = (JsonArray)circles[0]!["geometry"]!["coordinates"]![0]!;
        Assert.Equal(GeoMath.CircleSegments + 1, ring.Count);
        Assert.Equal(ring[0]![0]!.GetValue<double>(), ring[^1]![0]!.GetValue<double>());
    }

    [Fact]
    public void GeoJson_bounds_are_null_without_points()
    {
        var empty = new MapLayer(Guid.NewGuid(), "Empty", "e.xlsx", []);
        Assert.Null(CoordinateLayerGeoJson.Bounds(empty));
        Assert.NotNull(CoordinateLayerGeoJson.Bounds(Layer(0, (1, 2))));
    }
}
