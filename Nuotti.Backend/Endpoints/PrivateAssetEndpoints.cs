using Nuotti.Backend.Assets;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Endpoints;

public sealed record CreatePrivateCatalogEntryRequest(string Title, string Artist);
public sealed record CreatePrivateAssetUploadRequest(
    string AssetType, string ContentType, long Size, AssetProvenance Provenance);
public sealed record PublishPrivateAssetRevisionRequest(string Sha256);

public static class PrivateAssetEndpoints
{
    public static void MapPrivateAssetEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/workspaces/{workspaceId}/catalog", async (
            HttpContext http, string workspaceId, CreatePrivateCatalogEntryRequest request,
            IWorkspaceAccessStore access, IPrivateAssetMetadataStore metadata, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (!Text(request.Title, 200) || !Text(request.Artist, 200)) return Invalid("title", "Title and artist are required.");
            return Results.Ok(await metadata.CreateEntryAsync(
                workspaceId, request.Title, request.Artist, selected.Principal.UserId, ct));
        });

        app.MapPost("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/asset-uploads", async (
            HttpContext http, string workspaceId, string catalogEntryId, CreatePrivateAssetUploadRequest request,
            IWorkspaceAccessStore access, IPrivateAssetMetadataStore metadata, IPrivateAssetObjectStore objects,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var normalized = NormalizeUpload(request);
            if (normalized is null) return Invalid("asset", "Asset metadata and complete, current rights provenance are required.");
            var draft = await metadata.CreateDraftAsync(workspaceId, catalogEntryId, normalized.AssetType,
                normalized.ContentType, normalized.Size, normalized.Provenance, selected.Principal.UserId, ct);
            if (draft is null) return Results.NotFound();
            try
            {
                var grant = await objects.CreateUploadGrantAsync(draft.Value.ObjectKey, normalized.ContentType, ct);
                return Results.Ok(new PrivateAssetUploadGrant(draft.Value.Revision, grant.Uri, grant.ExpiresAt));
            }
            catch (PrivateAssetGrantUnavailableException)
            {
                return Results.Problem("Direct private-asset grants are unavailable.", statusCode: 503);
            }
        });

        app.MapPost("/v1/workspaces/{workspaceId}/revisions/{revisionId}/publish", async (
            HttpContext http, string workspaceId, string revisionId, PublishPrivateAssetRevisionRequest request,
            IWorkspaceAccessStore access, IPrivateAssetMetadataStore metadata, IPrivateAssetObjectStore objects,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (!ValidSha(request.Sha256)) return Invalid("sha256", "A lowercase or uppercase SHA-256 hex digest is required.");
            var revision = await metadata.GetAsync(workspaceId, revisionId, ct);
            if (revision is null) return Results.NotFound();
            if (revision.Status != AssetRevisionStatus.Draft || RightsExpired(revision.Provenance))
                return ProblemResults.Conflict("Revision cannot be published.",
                    "The revision is not a publishable draft with current usage rights.", ReasonCode.InvalidStateTransition);
            var claim = await metadata.TryBeginFinalizationAsync(workspaceId, revisionId, ct);
            if (claim is null) return ProblemResults.Conflict("Revision cannot be published.",
                "Another publication attempt already claimed this revision.", ReasonCode.InvalidStateTransition);
            revision = claim.Revision;
            SealedPrivateObject? sealedObject = null;
            var completed = false;
            try
            {
                var key = await metadata.GetObjectKeyAsync(workspaceId, revisionId, ct);
                if (key is null) return Results.NotFound();
                sealedObject = await objects.SealAsync(key, ct);
                if (sealedObject is null
                    || sealedObject.Evidence.Size != revision.DeclaredSize
                    || !sealedObject.Evidence.ContentType.Equals(revision.ContentType, StringComparison.OrdinalIgnoreCase)
                    || !sealedObject.Evidence.Sha256.Equals(request.Sha256, StringComparison.OrdinalIgnoreCase))
                    return ProblemResults.Conflict("Uploaded object evidence does not match.",
                        "Stored bytes, size, content type, or digest differ from the immutable draft metadata.",
                        ReasonCode.InvalidStateTransition);
                var published = await metadata.PublishAsync(workspaceId, revisionId, sealedObject.ObjectKey,
                    claim.Token, sealedObject.Evidence.Size, sealedObject.Evidence.Sha256, ct);
                completed = published is not null;
                return published is null
                    ? ProblemResults.Conflict("Revision cannot be published.",
                        "The revision is no longer a publishable draft.", ReasonCode.InvalidStateTransition)
                    : Results.Ok(published);
            }
            finally
            {
                if (!completed)
                {
                    try
                    {
                        if (sealedObject is not null)
                            await objects.DiscardSealedAsync(sealedObject.ObjectKey, CancellationToken.None);
                    }
                    finally
                    {
                        await metadata.CancelFinalizationAsync(workspaceId, revisionId, claim.Token, CancellationToken.None);
                    }
                }
            }
        });

        app.MapGet("/v1/workspaces/{workspaceId}/revisions/{revisionId}", async (
            HttpContext http, string workspaceId, string revisionId, IWorkspaceAccessStore access,
            IPrivateAssetMetadataStore metadata, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var revision = await metadata.GetAsync(workspaceId, revisionId, ct);
            return revision is null ? Results.NotFound() : Results.Ok(revision);
        });

        app.MapPost("/v1/workspaces/{workspaceId}/revisions/{revisionId}/download", async (
            HttpContext http, string workspaceId, string revisionId, IWorkspaceAccessStore access,
            IPrivateAssetMetadataStore metadata, IPrivateAssetObjectStore objects, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var revision = await metadata.GetAsync(workspaceId, revisionId, ct);
            if (revision?.Status != AssetRevisionStatus.Published || RightsExpired(revision.Provenance)) return Results.NotFound();
            var key = await metadata.GetObjectKeyAsync(workspaceId, revisionId, ct);
            if (key is null) return Results.NotFound();
            try
            {
                var grant = await objects.CreateDownloadGrantAsync(key, ct);
                return Results.Ok(new PrivateAssetDownloadGrant(grant.Uri, grant.ExpiresAt));
            }
            catch (PrivateAssetGrantUnavailableException)
            {
                return Results.Problem("Direct private-asset grants are unavailable.", statusCode: 503);
            }
        });

        app.MapDelete("/v1/workspaces/{workspaceId}/revisions/{revisionId}", async (
            HttpContext http, string workspaceId, string revisionId, IWorkspaceAccessStore access,
            IPrivateAssetMetadataStore metadata, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access?.Role != WorkspaceRole.Owner) return Results.NotFound();
            var archived = await metadata.ArchiveAsync(workspaceId, revisionId, ct);
            return archived is null ? Results.NotFound() : Results.Ok(archived);
        });
    }

    static readonly IReadOnlyDictionary<string, string[]> AssetUses = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["backing-track"] = ["backing-track", "live-performance"],
        ["click-track"] = ["click-track"],
        ["visual-hint"] = ["visual-hint", "projector-media"],
        ["image"] = ["visual-hint", "projector-media"],
        ["lyrics"] = ["lyrics", "projector-media"],
        ["video"] = ["projector-media"]
    };
    static readonly IReadOnlyDictionary<string, string[]> AssetContentTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["backing-track"] = ["audio/wav", "audio/mpeg", "audio/flac"],
        ["click-track"] = ["audio/wav", "audio/mpeg", "audio/flac"],
        ["visual-hint"] = ["image/png", "image/jpeg", "image/webp"],
        ["image"] = ["image/png", "image/jpeg", "image/webp"],
        ["lyrics"] = ["text/plain", "application/lrc"],
        ["video"] = ["video/mp4", "video/webm"]
    };
    static readonly HashSet<string> AllowedUses = AssetUses.Values.SelectMany(x => x).ToHashSet(StringComparer.Ordinal);

    static CreatePrivateAssetUploadRequest? NormalizeUpload(CreatePrivateAssetUploadRequest request)
    {
        var assetType = request.AssetType?.Trim().ToLowerInvariant();
        var contentType = request.ContentType?.Trim().ToLowerInvariant();
        var provenance = request.Provenance;
        if (assetType is null || !AssetUses.TryGetValue(assetType, out var compatibleUses)
            || !Text(contentType, 200) || !AssetContentTypes[assetType].Contains(contentType, StringComparer.Ordinal)
            || request.Size is <= 0 or > 2_000_000_000
            || !Text(provenance.Source, 500) || !Text(provenance.RightsBasis, 500)
            || !Text(provenance.Territory, 100) || RightsExpired(provenance)
            || !Text(provenance.SupportingDocumentReference, 500)
            || provenance.PermittedUses is not { Count: > 0 and <= 8 }) return null;
        var uses = provenance.PermittedUses.Select(x => x?.Trim().ToLowerInvariant()).ToArray();
        if (uses.Any(x => !Text(x, 80) || !AllowedUses.Contains(x!))
            || uses.Distinct(StringComparer.Ordinal).Count() != uses.Length
            || !uses.Any(x => compatibleUses.Contains(x, StringComparer.Ordinal))) return null;
        var normalizedProvenance = provenance with
        {
            Source = provenance.Source.Trim(), RightsBasis = provenance.RightsBasis.Trim(),
            Territory = provenance.Territory.Trim().ToUpperInvariant(), PermittedUses = uses!,
            SupportingDocumentReference = provenance.SupportingDocumentReference!.Trim()
        };
        return request with { AssetType = assetType, ContentType = contentType!, Provenance = normalizedProvenance };
    }
    static bool RightsExpired(AssetProvenance provenance) => provenance.RightsExpiresAt is { } expiry
        && expiry <= DateTimeOffset.UtcNow;
    static bool ValidSha(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
    static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    static IResult Invalid(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] });
}
