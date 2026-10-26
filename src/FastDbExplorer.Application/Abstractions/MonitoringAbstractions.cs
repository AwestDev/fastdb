using FastDbExplorer.Domain;

namespace FastDbExplorer.Application.Abstractions;

/// <summary>Where monitor definitions live. Never the monitored database (the app is read-only there).</summary>
public interface IMonitorStore
{
    Task<IReadOnlyList<MonitorDefinition>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<MonitorDefinition> monitors, CancellationToken ct = default);
}

/// <summary>Reads MAX(watermark column) and the server clock (one cheap, read-only query).</summary>
public interface IWatermarkReader
{
    Task<WatermarkReading> ReadAsync(ConnectionSettings settings, string database, string schema, string table, string column, CancellationToken ct);
}

/// <summary>
/// Writes the matching records to a report file and returns its full path. A very large report is written as CSV in several
/// files instead (see <see cref="ExportLimits"/>); the returned path is then the first file.
/// </summary>
public interface IReportWriter
{
    Task<string> WriteAsync(
        string monitorName, DateTime checkedAtUtc, IReadOnlyList<string> columns,
        IReadOnlyList<object?[]> rows, string folder, CancellationToken ct);
}

/// <summary>
/// Streams every row of a read-only query result into the file the user chose. A normal result becomes an Excel workbook;
/// a very big one is written as CSV, split into several files, so no single file gets heavy (see <see cref="ExportLimits"/>).
/// </summary>
public interface IQueryReportWriter
{
    Task<QueryExportResult> WriteQueryAsync(
        IReadOnlyList<string> columns, IAsyncEnumerable<object?[]> rows, string path, CancellationToken ct);
}

/// <summary>Runs one check of one monitor: finds new records that match the conditions and writes the report.</summary>
public interface IMonitorCheckService
{
    Task<MonitorCheckResult> CheckAsync(ConnectionSettings settings, MonitorDefinition monitor, CancellationToken ct);
}
