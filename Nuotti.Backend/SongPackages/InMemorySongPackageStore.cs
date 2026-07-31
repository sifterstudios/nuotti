namespace Nuotti.Backend.SongPackages;

public sealed class InMemorySongPackageStore(TimeProvider? timeProvider = null) : ISongPackageStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly object _gate = new();
    readonly Dictionary<(string WorkspaceId, string CatalogEntryId), SongPackageDraft> _drafts = [];
    readonly Dictionary<(string WorkspaceId, string CatalogEntryId), List<SongPackageRevision>> _revisions = [];

    public Task<SongPackageDraft> SaveDraftAsync(string workspaceId, string catalogEntryId,
        SongPackageDocument document, string userId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var draft = new SongPackageDraft(workspaceId, catalogEntryId, document, userId, _time.GetUtcNow());
            _drafts[(workspaceId, catalogEntryId)] = draft;
            return Task.FromResult(draft);
        }
    }

    public Task<SongPackageDraft?> GetDraftAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult<SongPackageDraft?>(
            _drafts.GetValueOrDefault((workspaceId, catalogEntryId)));
    }

    public Task<SongPackageRevision> PublishAsync(string workspaceId, string catalogEntryId,
        SongPackageDocument document, string revisionNote, IReadOnlyList<string> acceptedWarningCodes, string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var key = (workspaceId, catalogEntryId);
            if (!_revisions.TryGetValue(key, out var revisions)) _revisions[key] = revisions = [];
            var revision = new SongPackageRevision(workspaceId, catalogEntryId, $"pkg_{Guid.NewGuid():N}",
                revisions.Count + 1, document, revisionNote.Trim(), userId, _time.GetUtcNow(),
                acceptedWarningCodes.Order(StringComparer.Ordinal).ToArray());
            revisions.Add(revision);
            return Task.FromResult(revision);
        }
    }

    public Task<IReadOnlyList<SongPackageRevision>> GetRevisionsAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<SongPackageRevision>>(
            _revisions.GetValueOrDefault((workspaceId, catalogEntryId))?.ToArray() ?? []);
    }
}
