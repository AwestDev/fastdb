using FastDbExplorer.Domain;
using Microsoft.Data.SqlClient;

namespace FastDbExplorer.Infrastructure;

/// <summary>
/// Asks SQL Server whether the login can change data in its default database: a write-capable server role, a write-capable
/// database role, or a database permission that allows changes. Read-only: one SELECT through <see cref="ReadOnlySqlGuard"/>.
/// Limits: it looks at the default database only, and it does not see single-table grants (GRANT INSERT ON a table).
/// </summary>
public static class SqlServerAccessProbe
{
    private const string WriteAccessSql = """
        SELECT
            CAST(ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0) AS int),
            CAST(ISNULL(IS_MEMBER('db_owner'), 0) AS int),
            CAST(ISNULL(IS_MEMBER('db_datawriter'), 0) AS int),
            CAST(ISNULL(IS_MEMBER('db_ddladmin'), 0) AS int),
            CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.fn_my_permissions(NULL, 'DATABASE')
                 WHERE permission_name IN ('CONTROL', 'ALTER', 'CREATE TABLE')) THEN 1 ELSE 0 END AS int)
        """;

    /// <returns>True when the login can write, false when it cannot, null when the check itself could not run.</returns>
    /// <remarks>The check is best-effort: a failure returns null and never blocks the connection. Cancellation still propagates.</remarks>
    public static async Task<bool?> CanWriteAsync(ConnectionSettings settings, CancellationToken ct)
    {
        ReadOnlySqlGuard.EnsureReadOnly(WriteAccessSql);
        try
        {
            await using var connection = new SqlConnection(SqlConnectionStringFactory.Create(settings));
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = WriteAccessSql;
            command.CommandTimeout = 15;

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            for (var i = 0; i < reader.FieldCount; i++)
                if (reader.GetInt32(i) == 1) return true;
            return false;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return null;
        }
    }
}
