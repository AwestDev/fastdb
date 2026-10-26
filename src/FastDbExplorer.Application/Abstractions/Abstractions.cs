using FastDbExplorer.Domain;

namespace FastDbExplorer.Application.Abstractions;

/// <summary>Read-only access to server data. Implementations must never modify anything.</summary>
public interface IDatabaseMetadataService
{
    Task TestConnectionAsync(ConnectionSettings settings, CancellationToken ct);
    Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(ConnectionSettings settings, CancellationToken ct);
    Task<IReadOnlyList<TableInfo>> GetTablesAsync(ConnectionSettings settings, string database, CancellationToken ct);
    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(ConnectionSettings settings, string database, string schema, string table, CancellationToken ct);
    Task<PageResult> GetPageAsync(ConnectionSettings settings, PageRequest request, CancellationToken ct);
}

public interface IConnectionProfileStore
{
    Task<IReadOnlyList<SavedConnection>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(SavedConnection connection, CancellationToken ct = default);
    Task RemoveAsync(SavedConnection connection, CancellationToken ct = default);
}
