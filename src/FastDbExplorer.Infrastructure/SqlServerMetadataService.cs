using System.Data;
using System.Collections.Concurrent;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using Microsoft.Data.SqlClient;

namespace FastDbExplorer.Infrastructure;

public sealed class SqlServerMetadataService : IDatabaseMetadataService
{
    // 0 = no client-side command timeout. Large tables (hundreds of thousands of rows) must not be
    // abandoned after a fixed time; the user stops a search through its CancellationToken instead.
    private const int CommandTimeoutSeconds = 0;
    private const string PagingCapabilitiesSql = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')), CONVERT(int, DATABASEPROPERTYEX(DB_NAME(), 'CompatibilityLevel'))";
    private readonly ConcurrentDictionary<string, PagingCapabilities> _pagingCapabilities = new(StringComparer.OrdinalIgnoreCase);

    // System databases (ids 1-4) are hidden: this tool is for user data.
    private const string DatabasesSql =
        "SELECT name FROM sys.databases WHERE database_id > 4 AND state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name";

    // sys.partitions is cheap metadata (no table scan), so row counts are approximate but instant.
    private const string TablesSql = """
        SELECT s.name, t.name, SUM(p.rows)
        FROM sys.tables AS t
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
        GROUP BY s.name, t.name
        ORDER BY s.name, t.name
        """;

    private const string ColumnsSql = """
        SELECT c.name, TYPE_NAME(c.system_type_id), ISNULL(k.key_ordinal, 0)
        FROM sys.columns AS c
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id, ic.key_ordinal
            FROM sys.indexes AS i
            JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 1) AS k ON k.object_id = c.object_id AND k.column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(@fullName)
        ORDER BY c.column_id
        """;

    public async Task TestConnectionAsync(ConnectionSettings settings, CancellationToken ct)
        => await QueryAsync(settings, null, "SELECT 1", null, r => r.GetInt32(0), ct);

    public async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(ConnectionSettings settings, CancellationToken ct)
        => await QueryAsync(settings, null, DatabasesSql, null, r => new DatabaseInfo(r.GetString(0)), ct);

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(ConnectionSettings settings, string database, CancellationToken ct)
        => await QueryAsync(settings, database, TablesSql, null,
            r => new TableInfo(r.GetString(0), r.GetString(1), r.GetInt64(2)), ct);

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(
        ConnectionSettings settings, string database, string schema, string table, CancellationToken ct)
    {
        var fullName = SelectQueryBuilder.Quote(schema) + "." + SelectQueryBuilder.Quote(table);
        QueryParameter[] parameters = [new("@fullName", SqlDbType.NVarChar, fullName)];
        return await QueryAsync(settings, database, ColumnsSql, parameters,
            r => new ColumnInfo(r.GetString(0), r.GetString(1), Convert.ToInt32(r.GetValue(2))), ct);
    }

    public async Task<PageResult> GetPageAsync(ConnectionSettings settings, PageRequest request, CancellationToken ct)
    {
        var columns = await GetColumnsAsync(settings, request.Database, request.Schema, request.Table, ct);
        var cacheKey = PagingCapabilitiesCacheKey(settings, request.Database);
        var capabilities = await GetPagingCapabilitiesAsync(settings, request.Database, ct);
        var pageSize = Math.Clamp(request.PageSize, 1, SelectQueryBuilder.MaxPageSize);
        var keyCount = request.UseKeyset ? request.OrderBy.Count : 0;
        var valueCount = request.Columns.Count;

        async Task<PageResult> ExecutePageAsync(bool supportsOffsetFetch)
        {
            var query = SelectQueryBuilder.Build(request, columns, supportsOffsetFetch);
            return await ExecuteAsync(settings, request.Database, query.Sql, query.Parameters, async (reader, token) =>
            {
                var rows = new List<object?[]>();
                object?[]? lastKey = null;
                var hasMore = false;

                while (await reader.ReadAsync(token))
                {
                    if (rows.Count == pageSize) { hasMore = true; break; } // the +1 row only proves a next page exists

                    var values = new object?[valueCount];
                    for (var i = 0; i < valueCount; i++)
                    {
                        var v = reader.GetValue(i);
                        values[i] = v is DBNull ? null : v;
                    }
                    rows.Add(values);

                    if (keyCount > 0)
                    {
                        var key = new object?[keyCount];
                        for (var k = 0; k < keyCount; k++) key[k] = reader.GetValue(valueCount + k);
                        lastKey = key;
                    }
                }
                return new PageResult(request.Columns, rows, hasMore, lastKey);
            }, ct);
        }

        if (!capabilities.SupportsOffsetFetch)
        {
            if (!capabilities.SupportsRowNumber)
                throw UnsupportedPaging(capabilities);
            return await ExecuteLegacyPageAsync(capabilities);
        }

        try
        {
            return await ExecutePageAsync(true);
        }
        catch (DatabaseAccessException ex) when (IsOffsetFetchSyntaxError(ex))
        {
            _pagingCapabilities.TryRemove(cacheKey, out _);
            PagingCapabilities refreshed;
            try
            {
                refreshed = await GetPagingCapabilitiesAsync(settings, request.Database, ct);
            }
            catch (DatabaseAccessException diagnosticError)
            {
                throw new DatabaseAccessException(
                    DatabaseErrorKind.PagingDetectionFailed,
                    $"The server rejected OFFSET/FETCH: {ex.Message}{Environment.NewLine}Capability check failed: {diagnosticError.Message}",
                    diagnosticError);
            }

            if (refreshed.SupportsRowNumber)
            {
                if (refreshed.SupportsOffsetFetch)
                    refreshed = refreshed with { UseRowNumberFallback = true };
                _pagingCapabilities[cacheKey] = refreshed;
                return await ExecuteLegacyPageAsync(refreshed);
            }
            throw UnsupportedPaging(refreshed, ex.Message);
        }

        async Task<PageResult> ExecuteLegacyPageAsync(PagingCapabilities detected)
        {
            try
            {
                return await ExecutePageAsync(false);
            }
            catch (DatabaseAccessException ex)
            {
                throw new DatabaseAccessException(DatabaseErrorKind.PagingFallbackFailed,
                    $"Detected SQL Server version {detected.ProductVersion} (major {detected.MajorVersion}) and compatibility level {detected.CompatibilityLevel}. The read-only ROW_NUMBER paging query failed: {ex.Message}", ex);
            }
        }
    }

    private async Task<PagingCapabilities> GetPagingCapabilitiesAsync(
        ConnectionSettings settings, string database, CancellationToken ct)
    {
        var key = PagingCapabilitiesCacheKey(settings, database);
        if (_pagingCapabilities.TryGetValue(key, out var cached)) return cached;

        List<PagingProbe> rows;
        try
        {
            rows = await QueryAsync(settings, database, PagingCapabilitiesSql, null,
                r => new PagingProbe(r.IsDBNull(0) ? null : r.GetString(0),
                    r.IsDBNull(1) ? null : Convert.ToInt32(r.GetValue(1))), ct);
        }
        catch (DatabaseAccessException ex)
        {
            throw new DatabaseAccessException(DatabaseErrorKind.PagingDetectionFailed,
                $"Could not inspect SQL Server version and compatibility level using a read-only query. {ex.Message}", ex);
        }

        var probe = rows.SingleOrDefault();
        if (probe is null
            || probe.ProductVersion is null
            || probe.CompatibilityLevel is not int compatibilityLevel
            || !Version.TryParse(probe.ProductVersion, out var version))
            throw new DatabaseAccessException(DatabaseErrorKind.PagingDetectionFailed,
                "SQL Server did not return a valid product version and database compatibility level.");

        var capabilities = new PagingCapabilities(probe.ProductVersion, version.Major, compatibilityLevel);
        _pagingCapabilities[key] = capabilities;
        return capabilities;
    }

    private static string PagingCapabilitiesCacheKey(ConnectionSettings settings, string database)
        => settings.Server.Trim() + "\0" + database;

    private static bool IsOffsetFetchSyntaxError(DatabaseAccessException ex)
        => ex.InnerException is SqlException sql
           && (sql.Number is 102 or 153)
           && (sql.Message.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)
               || sql.Message.Contains("FETCH", StringComparison.OrdinalIgnoreCase));

    private static DatabaseAccessException UnsupportedPaging(PagingCapabilities capabilities, string? serverError = null)
    {
        var message = $"Detected SQL Server version {capabilities.ProductVersion} (major {capabilities.MajorVersion}) and database compatibility level {capabilities.CompatibilityLevel}. OFFSET/FETCH requires SQL Server 2012+ and compatibility level 110+; the read-only ROW_NUMBER fallback requires SQL Server 2005+ and compatibility level 90+.";
        if (!string.IsNullOrWhiteSpace(serverError)) message += $" SQL Server error: {serverError}";
        return new DatabaseAccessException(DatabaseErrorKind.PagingNotSupported, message);
    }

    private sealed record PagingProbe(string? ProductVersion, int? CompatibilityLevel);

    private sealed record PagingCapabilities(
        string ProductVersion, int MajorVersion, int CompatibilityLevel, bool UseRowNumberFallback = false)
    {
        public bool SupportsOffsetFetch => !UseRowNumberFallback && MajorVersion >= 11 && CompatibilityLevel >= 110;
        public bool SupportsRowNumber => MajorVersion >= 9 && CompatibilityLevel >= 90;
    }

    private static Task<List<T>> QueryAsync<T>(
        ConnectionSettings settings, string? database, string sql, IReadOnlyList<QueryParameter>? parameters,
        Func<SqlDataReader, T> map, CancellationToken ct)
        => ExecuteAsync(settings, database, sql, parameters, async (reader, token) =>
        {
            var result = new List<T>();
            while (await reader.ReadAsync(token))
                result.Add(map(reader));
            return result;
        }, ct);

    private static async Task<T> ExecuteAsync<T>(
        ConnectionSettings settings, string? database, string sql, IReadOnlyList<QueryParameter>? parameters,
        Func<SqlDataReader, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        ReadOnlySqlGuard.EnsureReadOnly(sql);
        try
        {
            await using var connection = new SqlConnection(SqlConnectionStringFactory.Create(settings, database));
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeoutSeconds;
            if (parameters is not null)
                foreach (var p in parameters) command.Parameters.Add(ToSqlParameter(p));

            // SequentialAccess streams rows; readers must read columns in order.
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            return await read(reader, ct);
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

    private static SqlParameter ToSqlParameter(QueryParameter p)
    {
        var parameter = new SqlParameter(p.Name, p.Type) { Value = p.Value };
        var length = (p.Value as string)?.Length ?? 0;
        if (p.Type == SqlDbType.NVarChar) parameter.Size = length <= 4000 ? 4000 : -1; // fixed sizes = fewer cached plans
        if (p.Type == SqlDbType.VarChar) parameter.Size = length <= 8000 ? 8000 : -1;
        if (p.Type == SqlDbType.Decimal && p.Value is decimal d)
        {
            parameter.Precision = 38;
            parameter.Scale = (byte)((decimal.GetBits(d)[3] >> 16) & 0xFF);
        }
        return parameter;
    }

    private static DatabaseErrorKind Classify(SqlException ex) => ex.Number switch
    {
        18456 or 4060 or 18452 => DatabaseErrorKind.LoginFailed,
        -2 => DatabaseErrorKind.Timeout,
        -1 or 2 or 40 or 53 or 258 or 10060 or 10061 => DatabaseErrorKind.ServerUnreachable,
        _ => DatabaseErrorKind.Other
    };
}
