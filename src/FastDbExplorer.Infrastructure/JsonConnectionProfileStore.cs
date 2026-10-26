using System.Text.Json;
using System.Text.Json.Serialization;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure;

/// <summary>Stores named connection profiles (never passwords) in %AppData%\FastDbExplorer\connections.json.</summary>
public sealed class JsonConnectionProfileStore : IConnectionProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public JsonConnectionProfileStore(string? path = null)
        => _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastDbExplorer", "connections.json");

    public async Task<IReadOnlyList<SavedConnection>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return [];
        try
        {
            List<SavedConnection> stored;
            await using (var stream = File.OpenRead(_path))
                stored = await JsonSerializer.DeserializeAsync<List<SavedConnection>>(stream, Options, ct) ?? [];

            var profiles = Normalize(stored);
            if (!stored.SequenceEqual(profiles)) await WriteAsync(profiles, ct);
            return profiles;
        }
        catch (JsonException)
        {
            return []; // A corrupt history file must never block the app; it is rewritten on next save.
        }
    }

    public async Task SaveAsync(SavedConnection connection, CancellationToken ct = default)
    {
        var profile = connection.Id == Guid.Empty ? connection with { Id = Guid.NewGuid() } : connection;
        var items = (await LoadAsync(ct)).Where(c => !IsSame(c, connection)).ToList();
        items.Insert(0, profile);
        await WriteAsync(items, ct);
    }

    public async Task RemoveAsync(SavedConnection connection, CancellationToken ct = default)
    {
        var items = (await LoadAsync(ct)).Where(c => !IsSame(c, connection)).ToList();
        await WriteAsync(items, ct);
    }

    private async Task WriteAsync(List<SavedConnection> items, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, items, Options, ct);
    }

    private static bool IsSame(SavedConnection a, SavedConnection b) =>
        a.Id != Guid.Empty && b.Id != Guid.Empty
            ? a.Id == b.Id
            : string.Equals(a.Server, b.Server, StringComparison.OrdinalIgnoreCase)
              && a.Authentication == b.Authentication
              && string.Equals(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase);

    private static List<SavedConnection> Normalize(IEnumerable<SavedConnection> stored)
    {
        var seenIds = new HashSet<Guid>();
        return stored.Select(profile =>
        {
            var id = profile.Id;
            if (id == Guid.Empty || !seenIds.Add(id))
            {
                id = Guid.NewGuid();
                seenIds.Add(id);
            }

            var name = string.IsNullOrWhiteSpace(profile.Name)
                ? profile.Authentication == AuthenticationMode.SqlServer && !string.IsNullOrWhiteSpace(profile.UserName)
                    ? $"{profile.Server} ({profile.UserName})"
                    : profile.Server
                : profile.Name.Trim();
            return profile with { Id = id, Name = name };
        }).ToList();
    }
}
