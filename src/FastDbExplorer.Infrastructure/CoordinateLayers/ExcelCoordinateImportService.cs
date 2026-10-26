using System.Globalization;
using System.Text;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure.CoordinateLayers;

/// <summary>
/// Imports points from the FIRST worksheet of an .xlsx/.xls file.
/// The first non-blank row is the header and must contain Name, Latitude and Longitude
/// (case-insensitive, surrounding spaces ignored, any order, extra columns ignored).
/// Coordinates are WGS84 decimal degrees. Rows are streamed; no DataTable is built.
/// Invalid rows are skipped and reported; a file-level problem yields no layer.
/// </summary>
public sealed class ExcelCoordinateImportService : IExcelImportService
{
    public const string NameColumn = "Name";
    public const string LatitudeColumn = "Latitude";
    public const string LongitudeColumn = "Longitude";

    /// <summary>At most this many issues are returned; <see cref="CoordinateImportResult.IssuesTruncated"/> says if more existed.</summary>
    public const int MaxReportedIssues = 1000;

    static ExcelCoordinateImportService()
    {
        // Old .xls files may use legacy code pages; .NET needs the provider registered once.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<CoordinateImportResult> ImportAsync(string filePath, CancellationToken ct = default)
        => Task.Run(() => Import(filePath, ct), ct);

    /// <summary>Synchronous core (also used by the tests).</summary>
    public CoordinateImportResult Import(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Fail(ImportIssueCode.FileNotFound, "No file path was given.");

        var extension = Path.GetExtension(filePath);
        if (!IsSupportedExtension(extension))
            return Fail(ImportIssueCode.UnsupportedFileType,
                $"Unsupported file type '{extension}'. Only .xlsx and .xls files can be imported.");

        if (!File.Exists(filePath))
            return Fail(ImportIssueCode.FileNotFound, $"File not found: {filePath}");

        try
        {
            // FileShare.ReadWrite: the file may still be open in Excel.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read(stream, Path.GetFileName(filePath), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidPasswordException)
        {
            return Fail(ImportIssueCode.PasswordProtected, "The workbook is password protected.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(ImportIssueCode.FileUnreadable, ex.Message);
        }
        catch (Exception ex)
        {
            // The Excel parsers throw several unrelated exception types for damaged or non-Excel content.
            return Fail(ImportIssueCode.FileCorrupt, $"The file could not be read as an Excel workbook: {ex.Message}");
        }
    }

    private static bool IsSupportedExtension(string extension)
        => extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
           || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);

    private static CoordinateImportResult Read(Stream stream, string fileName, CancellationToken ct)
    {
        // CreateReader detects .xls vs .xlsx from the content, so an .xlsx saved with an .xls extension still works.
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var issues = new IssueLog();
        var layerId = Guid.NewGuid();
        var points = new List<MapPoint>();
        Columns? columns = null;
        var rowNumber = 0;
        var rowsRead = 0;

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            rowNumber++;
            if (IsBlank(reader)) continue;

            if (columns is null)
            {
                columns = ReadHeader(reader, rowNumber, issues);
                if (columns is null) return issues.Failure(0);
                continue;
            }

            rowsRead++;
            ReadDataRow(reader, rowNumber, columns, layerId, issues, points);
        }

        if (columns is null)
        {
            issues.Add(ImportIssueCode.EmptySheet, null, null, "The first worksheet is empty.");
            return issues.Failure(0);
        }

        if (rowsRead == 0)
        {
            issues.Add(ImportIssueCode.NoDataRows, null, null, "The sheet has a header row but no data rows.");
            return issues.Failure(0);
        }

        if (points.Count == 0)
        {
            issues.Add(ImportIssueCode.NoValidRows, null, null, "None of the data rows contained a valid point.");
            return issues.Failure(rowsRead);
        }

        var layerName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(layerName)) layerName = fileName;
        return issues.Success(new MapLayer(layerId, layerName, fileName, points), rowsRead);
    }

    private sealed record Columns(int Name, int Latitude, int Longitude);

    private static Columns? ReadHeader(IExcelDataReader reader, int rowNumber, IssueLog issues)
    {
        var found = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase)
        {
            [NameColumn] = [],
            [LatitudeColumn] = [],
            [LongitudeColumn] = []
        };

        for (var c = 0; c < reader.FieldCount; c++)
        {
            var header = CleanHeader(CellToText(reader.GetValue(c)));
            if (found.TryGetValue(header, out var list)) list.Add(c);
        }

        var ok = true;
        foreach (var required in new[] { NameColumn, LatitudeColumn, LongitudeColumn })
        {
            var count = found[required].Count;
            if (count == 0)
            {
                issues.Add(ImportIssueCode.MissingColumn, rowNumber, required,
                    $"Required column '{required}' was not found in the header row.");
                ok = false;
            }
            else if (count > 1)
            {
                issues.Add(ImportIssueCode.DuplicateColumn, rowNumber, required,
                    $"Column '{required}' appears {count} times; it must appear exactly once.");
                ok = false;
            }
        }

        return ok ? new Columns(found[NameColumn][0], found[LatitudeColumn][0], found[LongitudeColumn][0]) : null;
    }

    private static void ReadDataRow(
        IExcelDataReader reader, int row, Columns columns, Guid layerId, IssueLog issues, List<MapPoint> points)
    {
        var valid = true;

        var name = CellToText(GetCell(reader, columns.Name)).Trim();
        if (name.Length == 0)
        {
            issues.Add(ImportIssueCode.EmptyName, row, NameColumn, "The Name cell is empty.");
            valid = false;
        }

        var latState = ReadNumber(GetCell(reader, columns.Latitude), out var latitude);
        valid &= CheckCoordinate(latState, latitude, row, LatitudeColumn,
            CoordinateRules.IsValidLatitude, ImportIssueCode.LatitudeOutOfRange, "-90 to 90", issues);

        var lonState = ReadNumber(GetCell(reader, columns.Longitude), out var longitude);
        valid &= CheckCoordinate(lonState, longitude, row, LongitudeColumn,
            CoordinateRules.IsValidLongitude, ImportIssueCode.LongitudeOutOfRange, "-180 to 180", issues);

        if (valid) points.Add(new MapPoint(Guid.NewGuid(), name, latitude, longitude, layerId));
    }

    private static bool CheckCoordinate(
        NumberState state, double value, int row, string column,
        Func<double, bool> isValid, ImportIssueCode outOfRange, string range, IssueLog issues)
    {
        switch (state)
        {
            case NumberState.Missing:
                issues.Add(ImportIssueCode.MissingCoordinate, row, column, $"The {column} cell is empty.");
                return false;
            case NumberState.NotANumber:
                issues.Add(ImportIssueCode.InvalidNumber, row, column,
                    $"The {column} value is not a decimal-degree number (degrees/minutes/seconds text is not supported yet).");
                return false;
            default:
                if (isValid(value)) return true;
                issues.Add(outOfRange, row, column,
                    $"{column} {value.ToString(CultureInfo.InvariantCulture)} is outside the valid range {range}.");
                return false;
        }
    }

    private enum NumberState { Ok, Missing, NotANumber }

    private static NumberState ReadNumber(object? cell, out double value)
    {
        value = 0;
        switch (cell)
        {
            case null or DBNull:
                return NumberState.Missing;
            case double d:
                value = d;
                return double.IsFinite(d) ? NumberState.Ok : NumberState.NotANumber;
            case int i:
                value = i;
                return NumberState.Ok;
            case long l:
                value = l;
                return NumberState.Ok;
            case float f:
                value = f;
                return double.IsFinite(value) ? NumberState.Ok : NumberState.NotANumber;
            case decimal m:
                value = (double)m;
                return NumberState.Ok;
            case string s:
                var text = NormalizeNumberText(s);
                if (text.Length == 0) return NumberState.Missing;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value)
                    ? NumberState.Ok
                    : NumberState.NotANumber;
            default:
                return NumberState.NotANumber;
        }
    }

    /// <summary>
    /// Makes numbers typed as text parseable: Persian/Arabic-Indic digits, the Arabic decimal separator,
    /// the Unicode minus, invisible direction marks, and a single decimal comma ("35,7") when no dot is present.
    /// Anything else (for example 35°41'20") is left alone and fails to parse.
    /// </summary>
    private static string NormalizeNumberText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Trim())
        {
            switch (ch)
            {
                case >= '\u0660' and <= '\u0669':
                    sb.Append((char)('0' + (ch - '\u0660')));
                    break;
                case >= '\u06F0' and <= '\u06F9':
                    sb.Append((char)('0' + (ch - '\u06F0')));
                    break;
                case '\u066B':
                    sb.Append('.');
                    break;
                case '\u2212':
                    sb.Append('-');
                    break;
                case '\u200E' or '\u200F' or '\u200B' or '\uFEFF':
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        var result = sb.ToString().Trim();
        if (result.Contains(',') && !result.Contains('.')) result = result.Replace(',', '.');
        return result;
    }

    private static object? GetCell(IExcelDataReader reader, int index)
        => index < reader.FieldCount ? reader.GetValue(index) : null;

    private static bool IsBlank(IExcelDataReader reader)
    {
        for (var c = 0; c < reader.FieldCount; c++)
            if (CellToText(reader.GetValue(c)).Trim().Length > 0) return false;
        return true;
    }

    private static string CellToText(object? cell) => cell switch
    {
        null or DBNull => "",
        string s => s,
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => cell.ToString() ?? ""
    };

    private static string CleanHeader(string text) => text.Replace("\uFEFF", "").Replace("\u200B", "").Trim();

    private static CoordinateImportResult Fail(ImportIssueCode code, string detail)
    {
        var log = new IssueLog();
        log.Add(code, null, null, detail);
        return log.Failure(0);
    }

    private sealed class IssueLog
    {
        private readonly List<ImportIssue> _items = [];
        private bool _truncated;

        public void Add(ImportIssueCode code, int? row, string? column, string detail)
        {
            if (_items.Count >= MaxReportedIssues)
            {
                _truncated = true;
                return;
            }

            _items.Add(new ImportIssue(code, row, column, detail));
        }

        public CoordinateImportResult Failure(int rowsRead) => new(null, _items, rowsRead, 0, _truncated);

        public CoordinateImportResult Success(MapLayer layer, int rowsRead)
            => new(layer, _items, rowsRead, layer.Points.Count, _truncated);
    }
}
