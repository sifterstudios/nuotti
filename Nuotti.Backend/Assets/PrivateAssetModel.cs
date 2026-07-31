using System.Text.Json.Serialization;

namespace Nuotti.Backend.Assets;

[JsonConverter(typeof(JsonStringEnumConverter<AssetRevisionStatus>))]
public enum AssetRevisionStatus { Draft, Finalizing, Published, Archived }

public sealed record AssetProvenance(
    string Source, string RightsBasis, string Territory,
    IReadOnlyList<string> PermittedUses, DateTimeOffset? RightsExpiresAt,
    string? SupportingDocumentReference);
public sealed record PrivateCatalogEntry(
    string CatalogEntryId, string WorkspaceId, string Title, string Artist,
    string CreatedBy, DateTimeOffset CreatedAt);
public sealed record PrivateAssetRevision(
    string RevisionId, string CatalogEntryId, string WorkspaceId, AssetRevisionStatus Status,
    string AssetType, string ContentType, long DeclaredSize, long? StoredSize, string? Sha256,
    AssetProvenance Provenance, string UploadedBy, DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt, DateTimeOffset? ArchivedAt);
public sealed record PrivateAssetUploadGrant(
    PrivateAssetRevision Revision, Uri UploadUri, DateTimeOffset ExpiresAt);
public sealed record PrivateAssetDownloadGrant(Uri DownloadUri, DateTimeOffset ExpiresAt);
public sealed record PrivateObjectEvidence(long Size, string ContentType, string Sha256, string ETag);
public sealed record SealedPrivateObject(string ObjectKey, PrivateObjectEvidence Evidence);

public interface IPrivateAssetMetadataStore
{
    Task<PrivateCatalogEntry> CreateEntryAsync(string workspaceId, string title, string artist, string userId,
        CancellationToken cancellationToken = default);
    Task<(PrivateAssetRevision Revision, string ObjectKey)?> CreateDraftAsync(
        string workspaceId, string catalogEntryId, string assetType, string contentType, long declaredSize,
        AssetProvenance provenance, string userId, CancellationToken cancellationToken = default);
    Task<PrivateAssetRevision?> TryBeginFinalizationAsync(string workspaceId, string revisionId,
        CancellationToken cancellationToken = default);
    Task CancelFinalizationAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default);
    Task<PrivateAssetRevision?> PublishAsync(string workspaceId, string revisionId, string sealedObjectKey,
        long storedSize, string sha256,
        CancellationToken cancellationToken = default);
    Task<PrivateAssetRevision?> GetAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default);
    Task<string?> GetObjectKeyAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default);
    Task<PrivateAssetRevision?> ArchiveAsync(string workspaceId, string revisionId, CancellationToken cancellationToken = default);
}

public interface IPrivateAssetObjectStore
{
    Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateUploadGrantAsync(string objectKey, string contentType,
        CancellationToken cancellationToken = default);
    Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateDownloadGrantAsync(string objectKey,
        CancellationToken cancellationToken = default);
    Task<SealedPrivateObject?> SealAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DiscardSealedAsync(string objectKey, CancellationToken cancellationToken = default);
}
