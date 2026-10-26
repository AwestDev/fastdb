namespace FastDbExplorer.Domain;

/// <summary>
/// Rules shared by the Excel import and the layer store.
/// Coordinates are WGS84 (EPSG:4326) decimal degrees; radii are always metres.
/// </summary>
public static class CoordinateRules
{
    /// <summary>Half of Earth's equatorial circumference: no two points are farther apart, so a bigger radius is meaningless.</summary>
    public const double MaxRadiusMeters = 20_037_508;

    public static bool IsValidLatitude(double value) => double.IsFinite(value) && value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value) => double.IsFinite(value) && value is >= -180 and <= 180;

    public static bool IsValidRadiusMeters(double value) => double.IsFinite(value) && value is >= 0 and <= MaxRadiusMeters;
}

/// <summary>One map point. <see cref="LayerId"/> ties it to its <see cref="MapLayer"/>.</summary>
public sealed record MapPoint(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    Guid LayerId,
    IReadOnlyDictionary<string, string>? Details = null);

/// <summary>
/// A named set of points imported from one file. The constructor enforces the invariants
/// (valid WGS84 coordinates, every point belongs to this layer, radius in metres), so an invalid layer cannot exist.
/// </summary>
public sealed class MapLayer
{
    /// <summary>0 = no radius. The unit is always metres.</summary>
    public const double DefaultRadiusMeters = 0;

    private string _name;
    private double _radiusMeters;

    /// <exception cref="ArgumentException">Empty id/name/file name, a point of another layer, a nameless point or an invalid coordinate.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Radius is negative, not finite or above <see cref="CoordinateRules.MaxRadiusMeters"/>.</exception>
    public MapLayer(
        Guid id,
        string name,
        string fileName,
        IEnumerable<MapPoint> points,
        bool visibility = true,
        double radiusMeters = DefaultRadiusMeters)
    {
        if (id == Guid.Empty) throw new ArgumentException("The layer id must not be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(points);

        var list = new List<MapPoint>();
        foreach (var point in points)
        {
            if (point is null)
                throw new ArgumentException("A point must not be null.", nameof(points));
            if (point.LayerId != id)
                throw new ArgumentException($"Point '{point.Name}' belongs to another layer.", nameof(points));
            if (string.IsNullOrWhiteSpace(point.Name))
                throw new ArgumentException("A point must have a name.", nameof(points));
            if (!CoordinateRules.IsValidLatitude(point.Latitude) || !CoordinateRules.IsValidLongitude(point.Longitude))
                throw new ArgumentException($"Point '{point.Name}' has coordinates outside the WGS84 range.", nameof(points));
            list.Add(point);
        }

        Id = id;
        _name = name;
        FileName = fileName;
        Points = list.AsReadOnly();
        Visibility = visibility;
        RadiusMeters = radiusMeters;
    }

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value;
        }
    }

    /// <summary>The source file name (no folder), kept for display only.</summary>
    public string FileName { get; }

    public IReadOnlyList<MapPoint> Points { get; }

    /// <summary>Whether the layer is shown on the map (rendering is a later phase).</summary>
    public bool Visibility { get; set; }

    /// <summary>Radius around each point, always in metres.</summary>
    public double RadiusMeters
    {
        get => _radiusMeters;
        set
        {
            if (!CoordinateRules.IsValidRadiusMeters(value))
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"The radius must be between 0 and {CoordinateRules.MaxRadiusMeters} metres.");
            _radiusMeters = value;
        }
    }

    /// <summary>Marker icon key from <see cref="MapMarkerCatalog"/>; null = the classic dot marker.</summary>
    public string? IconKey { get; set; }

    /// <summary>Marker colour as #RRGGBB; null = an automatic palette colour (see <see cref="MapMarkerCatalog.EffectiveColor"/>).</summary>
    public string? Color { get; set; }
}

/// <summary>Machine-readable import problems. The UI turns them into Persian text (same idea as DatabaseErrorKind).</summary>
public enum ImportIssueCode
{
    // File level: no layer is produced.
    UnsupportedFileType,
    FileNotFound,
    FileUnreadable,
    FileCorrupt,
    PasswordProtected,
    EmptySheet,
    MissingColumn,
    DuplicateColumn,
    NoDataRows,
    NoValidRows,

    // Row level: the row is skipped, the rest is still imported.
    EmptyName,
    MissingCoordinate,
    InvalidNumber,
    LatitudeOutOfRange,
    LongitudeOutOfRange
}

/// <param name="Code">What went wrong.</param>
/// <param name="Row">1-based row as counted by the reader (header row = first non-blank row); null for file-level problems.</param>
/// <param name="Column">Column name (Name / Latitude / Longitude) when it applies.</param>
/// <param name="Detail">English technical detail for logs and for the UI to show under the Persian text.</param>
public sealed record ImportIssue(ImportIssueCode Code, int? Row, string? Column, string Detail);

/// <summary>
/// Outcome of one import. <see cref="Layer"/> is null when the file as a whole could not be imported;
/// skipped rows are listed in <see cref="Issues"/> (capped, see <see cref="IssuesTruncated"/>).
/// </summary>
public sealed record CoordinateImportResult(
    MapLayer? Layer,
    IReadOnlyList<ImportIssue> Issues,
    int RowsRead,
    int RowsImported,
    bool IssuesTruncated)
{
    public bool Succeeded => Layer is not null;

    public int RowsSkipped => RowsRead - RowsImported;
}
