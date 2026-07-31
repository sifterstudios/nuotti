namespace Nuotti.Backend.SessionSnapshots;

public sealed class InMemorySessionSetlistSnapshotStore(TimeProvider? timeProvider = null)
    : ISessionSetlistSnapshotStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly object _gate = new();
    readonly Dictionary<(string Workspace, string Session), SessionSetlistSnapshot> _snapshots = [];

    public Task<SessionSetlistSnapshot?> GetAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_snapshots.GetValueOrDefault((workspaceId, sessionCode)));
    }

    public Task<SessionSetlistSnapshot> CreateAsync(string workspaceId, string sessionCode,
        IReadOnlyList<SessionSetlistItem> songs, ScoringPolicySnapshot scoringPolicy,
        IReadOnlyList<SnapshotAsset> assets, IReadOnlyList<string> acceptedWarnings, string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = (workspaceId, sessionCode);
            if (_snapshots.ContainsKey(key)) throw new InvalidOperationException("Session Setlist Snapshot already exists.");
            var snapshot = new SessionSetlistSnapshot($"snap_{Guid.NewGuid():N}", workspaceId, sessionCode, 1,
                songs.ToArray(), scoringPolicy, assets.ToArray(), acceptedWarnings.Order(StringComparer.Ordinal).ToArray(),
                userId, _time.GetUtcNow());
            _snapshots[key] = snapshot;
            return Task.FromResult(snapshot);
        }
    }
}
