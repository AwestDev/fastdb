using System.Data;
using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure;

namespace FastDbExplorer.Tests;

public class SpatialSearchTests
{
    private static readonly ColumnInfo[] Table =
    [
        new("Id", "int", 1),
        new("Name", "nvarchar", 0),
        new("Latitude", "float", 0),
        new("Longitude", "float", 0),
    ];

    [Fact]
    public void Detects_latitude_longitude_by_name_and_type()
    {
        var target = SpatialSearch.Detect(Table);
        Assert.NotNull(target);
        Assert.Equal(SpatialMode.LatLonColumns, target!.Mode);
        Assert.Equal("Latitude", target.LatColumn);
        Assert.Equal("Longitude", target.LonColumn);
    }

    [Fact]
    public void Prefers_geography_column_when_present()
    {
        ColumnInfo[] columns = [new("Id", "int", 1), new("Latitude", "float", 0), new("Longitude", "float", 0), new("Geo", "geography", 0)];
        var target = SpatialSearch.Detect(columns);
        Assert.Equal(SpatialMode.GeographyColumn, target!.Mode);
        Assert.Equal("Geo", target.GeographyColumn);
    }

    [Fact]
    public void Text_columns_are_not_coordinates()
    {
        ColumnInfo[] columns = [new("Lat", "nvarchar", 0), new("Lon", "nvarchar", 0)];
        Assert.Null(SpatialSearch.Detect(columns));
    }

    [Fact]
    public void Builds_parameterized_haversine_query_ordered_by_distance()
    {
        var target = SpatialSearch.Detect(Table)!;
        var query = SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id", "Name"], target, 35.7, 51.4, 5000, 50);

        Assert.Contains("TOP (@take)", query.Sql);
        Assert.Contains("[Latitude] IS NOT NULL", query.Sql);
        Assert.Contains("<= @radius", query.Sql);
        Assert.Contains("ORDER BY 2 * 6371000", query.Sql);
        Assert.DoesNotContain("35.7", query.Sql); // values are parameters, never inlined
        Assert.Contains(query.Parameters, p => p.Name == "@lat" && p.Type == SqlDbType.Float && (double)p.Value! == 35.7);
        Assert.Contains(query.Parameters, p => p.Name == "@radius" && (double)p.Value! == 5000);
    }

    [Fact]
    public void Rejects_invalid_centre_and_radius()
    {
        var target = SpatialSearch.Detect(Table)!;
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id"], target, 95, 0, 1000, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id"], target, 0, 200, 1000, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id"], target, 0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id"], target, 0, 0, 600_000, 10));
    }

    [Fact]
    public void Unknown_columns_are_rejected()
    {
        var target = new SpatialTarget(SpatialMode.LatLonColumns, "Nope", "Longitude", null);
        Assert.Throws<ArgumentException>(() => SpatialSearch.BuildNearby("dbo", "Places", Table, ["Id"], target, 0, 0, 1000, 10));
    }
}
