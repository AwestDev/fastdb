namespace FastDbExplorer.Application.Abstractions;

/// <summary>Remembers small map preferences between runs. Today that is only the last map file that opened successfully.</summary>
public interface IMapSettingsStore
{
    /// <returns>The saved map file path, or null when nothing was saved or the settings file is unreadable.</returns>
    Task<string?> LoadLastMapPathAsync(CancellationToken ct = default);

    /// <summary>Replaces the saved path. Callers should only pass a map that opened successfully.</summary>
    Task SaveLastMapPathAsync(string path, CancellationToken ct = default);
}
