namespace Nuotti.Backend.Assets;

public sealed class InMemoryPrivateAssetMetadataStore(TimeProvider? timeProvider = null) : IPrivateAssetMetadataStore
{
    sealed record StoredRevision(PrivateAssetRevision Revision, string ObjectKey,
        DateTimeOffset? FinalizingAt = null, string? FinalizationToken = null);
    readonly object _gate = new();
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, PrivateCatalogEntry> _entries = [];
    readonly Dictionary<string, StoredRevision> _revisions = [];

    public Task<PrivateCatalogEntry> CreateEntryAsync(string workspaceId, string title, string artist, string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var entry = new PrivateCatalogEntry($"entry_{Guid.NewGuid():N}", workspaceId, title.Trim(), artist.Trim(), userId, _time.GetUtcNow());
            _entries[entry.CatalogEntryId] = entry;
            return Task.FromResult(entry);
        }
    }

    public Task<(PrivateAssetRevision Revision, string ObjectKey)?> CreateDraftAsync(
        string workspaceId, string catalogEntryId, string assetType, string contentType, long declaredSize,
        AssetProvenance provenance, string userId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(catalogEntryId, out var entry) || entry.WorkspaceId != workspaceId)
                return Task.FromResult<(PrivateAssetRevision, string)?>(null);
            var revision = new PrivateAssetRevision($"rev_{Guid.NewGuid():N}", catalogEntryId, workspaceId,
                AssetRevisionStatus.Draft, assetType, contentType, declaredSize, null, null, provenance,
                userId, _time.GetUtcNow(), null, null);
            var key = $"asset_{Guid.NewGuid():N}";
            _revisions[revision.RevisionId] = new(revision, key);
            return Task.FromResult<(PrivateAssetRevision, string)?>(new(revision, key));
        }
    }

    public Task<PrivateAssetRevision?> PublishAsync(string workspaceId, string revisionId, string sealedObjectKey,
        string claimToken, long storedSize, string sha256,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_revisions.TryGetValue(revisionId, out var stored) || stored.Revision.WorkspaceId != workspaceId
                || stored.Revision.Status != AssetRevisionStatus.Finalizing || stored.FinalizationToken != claimToken
                || stored.Revision.DeclaredSize != storedSize)
                return Task.FromResult<PrivateAssetRevision?>(null);
            var published = stored.Revision with
            {
                Status = AssetRevisionStatus.Published, StoredSize = storedSize,
                Sha256 = sha256.ToLowerInvariant(), PublishedAt = _time.GetUtcNow()
            };
            _revisions[revisionId] = stored with
            {
                Revision = published, ObjectKey = sealedObjectKey, FinalizingAt = null, FinalizationToken = null
            };
            return Task.FromResult<PrivateAssetRevision?>(published);
        }
    }

    public Task<PrivateAssetFinalizationClaim?> TryBeginFinalizationAsync(string workspaceId, string revisionId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_revisions.TryGetValue(revisionId, out var stored) || stored.Revision.WorkspaceId != workspaceId
                || (stored.Revision.Status != AssetRevisionStatus.Draft
                    && !(stored.Revision.Status == AssetRevisionStatus.Finalizing
                        && stored.FinalizingAt <= _time.GetUtcNow().AddMinutes(-10))))
                return Task.FromResult<PrivateAssetFinalizationClaim?>(null);
            var finalizing = stored.Revision with { Status = AssetRevisionStatus.Finalizing };
            var token = Guid.NewGuid().ToString("N");
            _revisions[revisionId] = stored with
            {
                Revision = finalizing, FinalizingAt = _time.GetUtcNow(), FinalizationToken = token
            };
            return Task.FromResult<PrivateAssetFinalizationClaim?>(new(finalizing, token));
        }
    }

    public Task CancelFinalizationAsync(string workspaceId, string revisionId, string claimToken,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_revisions.TryGetValue(revisionId, out var stored) && stored.Revision.WorkspaceId == workspaceId
                && stored.Revision.Status == AssetRevisionStatus.Finalizing
                && stored.FinalizationToken == claimToken)
                _revisions[revisionId] = stored with
                {
                    Revision = stored.Revision with { Status = AssetRevisionStatus.Draft }, FinalizingAt = null,
                    FinalizationToken = null
                };
        }
        return Task.CompletedTask;
    }

    public Task<PrivateAssetRevision?> GetAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult<PrivateAssetRevision?>(_revisions.TryGetValue(revisionId, out var stored)
            && stored.Revision.WorkspaceId == workspaceId ? stored.Revision : null);
    }

    public Task<string?> GetObjectKeyAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_revisions.TryGetValue(revisionId, out var stored)
            && stored.Revision.WorkspaceId == workspaceId ? stored.ObjectKey : null);
    }

    public Task<PrivateAssetRevision?> ArchiveAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_revisions.TryGetValue(revisionId, out var stored) || stored.Revision.WorkspaceId != workspaceId
                || stored.Revision.Status != AssetRevisionStatus.Published) return Task.FromResult<PrivateAssetRevision?>(null);
            var archived = stored.Revision with { Status = AssetRevisionStatus.Archived, ArchivedAt = _time.GetUtcNow() };
            _revisions[revisionId] = stored with { Revision = archived };
            return Task.FromResult<PrivateAssetRevision?>(archived);
        }
    }
}

public sealed class InMemoryPrivateAssetObjectStore(TimeProvider? timeProvider = null) : IPrivateAssetObjectStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, byte[]> _objects = [];
    public Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateUploadGrantAsync(string objectKey, string contentType,
        CancellationToken cancellationToken = default) => Task.FromResult((new Uri($"https://objects.invalid/upload/{objectKey}?grant={Guid.NewGuid():N}"), _time.GetUtcNow().AddMinutes(5)));
    public Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateDownloadGrantAsync(string objectKey,
        CancellationToken cancellationToken = default) => Task.FromResult((new Uri($"https://objects.invalid/download/{objectKey}?grant={Guid.NewGuid():N}"), _time.GetUtcNow().AddMinutes(5)));
    readonly Dictionary<string, string> _contentTypes = [];
    public Task<SealedPrivateObject?> SealAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(objectKey, out var bytes)) return Task.FromResult<SealedPrivateObject?>(null);
        var sealedKey = $"sealed_{Guid.NewGuid():N}";
        _objects[sealedKey] = bytes.ToArray();
        var contentType = _contentTypes.GetValueOrDefault(objectKey, "application/octet-stream");
        _contentTypes[sealedKey] = contentType;
        var evidence = new PrivateObjectEvidence(bytes.LongLength, contentType,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            $"memory-{Guid.NewGuid():N}");
        return Task.FromResult<SealedPrivateObject?>(new(sealedKey, evidence));
    }
    public void PutDirect(string objectKey, byte[] bytes, string contentType = "application/octet-stream")
    {
        _objects[objectKey] = bytes;
        _contentTypes[objectKey] = contentType;
    }
    public byte[]? GetDirect(string objectKey) => _objects.TryGetValue(objectKey, out var bytes) ? bytes.ToArray() : null;
    public Task DiscardSealedAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.Remove(objectKey); _contentTypes.Remove(objectKey); return Task.CompletedTask;
    }
}
