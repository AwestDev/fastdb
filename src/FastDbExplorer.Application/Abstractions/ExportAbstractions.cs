namespace FastDbExplorer.Application.Abstractions;

/// <summary>
/// When a result is too big for one comfortable Excel workbook it is written as CSV, split into several files when needed.
/// Defaults: a workbook may hold about 2 million cells (100,000 rows of 20 columns); a CSV part holds at most 500,000 rows or 100 MB.
/// </summary>
public sealed record ExportLimits(
    long XlsxMaxCells = 2_000_000,
    int CsvMaxRowsPerFile = 500_000,
    long CsvMaxBytesPerFile = 100L * 1024 * 1024)
{
    /// <summary>Excel cannot hold more rows than this on one sheet (the header row takes one of 1,048,576).</summary>
    public const int ExcelMaxDataRows = 1_048_575;

    public static ExportLimits Default { get; } = new();

    /// <summary>How many data rows a workbook with this many columns may hold before CSV is used instead.</summary>
    public int MaxWorkbookRows(int columnCount)
        => (int)Math.Min(ExcelMaxDataRows, Math.Max(1, XlsxMaxCells / Math.Max(1, columnCount)));
}

public enum QueryExportFormat { Xlsx, Csv }

/// <summary>What an export produced: the format, every file written (in order) and the number of data rows.</summary>
public sealed record QueryExportResult(QueryExportFormat Format, IReadOnlyList<string> Files, long Rows)
{
    public bool IsSplit => Files.Count > 1;
}
