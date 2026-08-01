using Nuotti.Backend.Assets;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Catalog;

public interface ISharedSongCatalog
{
    IReadOnlyList<SongRef> All { get; }
    void Seed(IEnumerable<SongRef> songs);
}

public sealed class InMemorySharedSongCatalog : ISharedSongCatalog
{
    readonly object _gate = new();
    List<SongRef> _songs = [];

    public IReadOnlyList<SongRef> All
    {
        get { lock (_gate) return _songs; }
    }

    public void Seed(IEnumerable<SongRef> songs)
    {
        lock (_gate) _songs = songs.ToList();
    }
}

public interface IAudienceCatalogSearch
{
    Task<IReadOnlyList<SongRef>> SearchAsync(string sessionCode, string query, int limit = 25,
        CancellationToken cancellationToken = default);
}

public sealed class AudienceCatalogSearch(
    ISharedSongCatalog shared,
    IPrivateAssetMetadataStore privateCatalog,
    ISessionWorkspaceBinder binder) : IAudienceCatalogSearch
{
    public async Task<IReadOnlyList<SongRef>> SearchAsync(string sessionCode, string query, int limit = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionCode)) return [];
        limit = Math.Clamp(limit, 1, 50);
        var needle = (query ?? string.Empty).Trim();
        var workspaceId = binder.Resolve(sessionCode);
        var workspaceEntries = string.IsNullOrWhiteSpace(workspaceId)
            ? Array.Empty<PrivateCatalogEntry>()
            : await privateCatalog.ListEntriesAsync(workspaceId, cancellationToken);

        IEnumerable<SongRef> combined = shared.All;
        if (workspaceEntries.Count > 0)
        {
            combined = combined.Concat(workspaceEntries.Select(e =>
                new SongRef(new SongId(e.CatalogEntryId), e.Title, e.Artist)));
        }

        if (needle.Length == 0)
            return combined.Take(limit).ToArray();

        return combined
            .Where(s => Contains(s.Title, needle) || Contains(s.Artist, needle) || Contains(s.Id.Value, needle))
            .Take(limit)
            .ToArray();
    }

    static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
