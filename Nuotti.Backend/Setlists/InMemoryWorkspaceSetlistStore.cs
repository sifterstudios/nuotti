namespace Nuotti.Backend.Setlists;

public sealed class InMemoryWorkspaceSetlistStore(TimeProvider? timeProvider = null) : IWorkspaceSetlistStore
{
    readonly object _gate = new();
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, WorkspaceSetlist> _setlists = new(StringComparer.Ordinal);

    public Task<WorkspaceSetlist?> GetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult<WorkspaceSetlist?>(_setlists.TryGetValue(workspaceId, out var setlist) ? setlist : null);
    }

    public Task<WorkspaceSetlist> SaveAsync(string workspaceId, IReadOnlyList<SetlistSongSelection> songs, string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var setlist = new WorkspaceSetlist(workspaceId, songs.ToArray(), _time.GetUtcNow(), userId);
            _setlists[workspaceId] = setlist;
            return Task.FromResult(setlist);
        }
    }
}
