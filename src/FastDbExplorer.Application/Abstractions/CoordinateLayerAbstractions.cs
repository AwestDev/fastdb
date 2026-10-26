using FastDbExplorer.Domain;

namespace FastDbExplorer.Application.Abstractions;

/// <summary>Reads an Excel file (.xlsx / .xls) with the columns Name, Latitude, Longitude into a <see cref="MapLayer"/>.</summary>
public interface IExcelImportService
{
    /// <summary>Never throws for bad files: problems come back as <see cref="CoordinateImportResult.Issues"/>.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<CoordinateImportResult> ImportAsync(string filePath, CancellationToken ct = default);
}

/// <summary>Keeps imported layers across application restarts.</summary>
public interface ICoordinateLayerStore
{
    /// <summary>All readable layers. Damaged or unknown-version files are skipped, never deleted.</summary>
    Task<IReadOnlyList<MapLayer>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Creates or replaces the stored copy of the layer (also used after visibility/radius changes).</summary>
    Task SaveAsync(MapLayer layer, CancellationToken ct = default);

    /// <returns>true when a stored layer was deleted.</returns>
    Task<bool> RemoveAsync(Guid layerId, CancellationToken ct = default);
}
