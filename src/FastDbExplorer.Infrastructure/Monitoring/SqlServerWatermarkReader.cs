using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using Microsoft.Data.SqlClient;

namespace FastDbExplorer.Infrastructure.Monitoring;

/// <summary>
/// SELECT MAX(column), SYSDATETIME() on an increasing number / date column. With an index (primary key, created-at)
/// this is a single seek, so it adds almost no load. Read-only: the command goes through <see cref="ReadOnlySqlGuard"/>.
/// </summary>
public sealed class SqlServerWatermarkReader(IDatabaseMetadataService metadata) : IWatermarkReader
{
    private const int CommandTimeoutSeconds = 30;

    public async Task<WatermarkReading> ReadAsync(
        ConnectionSettings settings, string database, string schema, string table, string column, CancellationToken ct)
    {
        var columns = await metadata.GetColumnsAsync(settings, database, schema, table, ct);
        var info = columns.FirstOrDefault(c => c.Name == column)
                   ?? throw new ArgumentException($"Unknown column '{column}'.");
        if (!WatermarkTypes.IsSupported(info.TypeName))
            throw new NotSupportedException($"Column '{column}' ({info.TypeName}) cannot be used to detect new records.");

        var sql = $"SELECT MAX({SelectQueryBuilder.Quote(info.Name)}), SYSDATETIME() FROM {SelectQueryBuilder.Quote(schema)}.{SelectQueryBuilder.Quote(table)}";
        ReadOnlySqlGuard.EnsureReadOnly(sql);

        try
        {
            await using var connection = new SqlConnection(SqlConnectionStringFactory.Create(settings, database));
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeoutSeconds;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return new WatermarkReading(null, null);

            var max = await reader.IsDBNullAsync(0, ct) ? null : WatermarkTypes.Format(reader.GetValue(0));
            DateTime? now = await reader.IsDBNullAsync(1, ct) ? null : reader.GetDateTime(1);
            return new WatermarkReading(max, now);
        }
        catch (SqlException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (SqlException ex)
        {
            throw new DatabaseAccessException(Classify(ex), ex.Message, ex);
        }
    }

    private static DatabaseErrorKind Classify(SqlException ex) => ex.Number switch
    {
        18456 or 4060 or 18452 => DatabaseErrorKind.LoginFailed,
        -2 => DatabaseErrorKind.Timeout,
        -1 or 2 or 40 or 53 or 258 or 10060 or 10061 => DatabaseErrorKind.ServerUnreachable,
        _ => DatabaseErrorKind.Other
    };
}
