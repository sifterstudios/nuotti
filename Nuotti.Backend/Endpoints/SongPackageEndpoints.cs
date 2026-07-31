using Nuotti.Backend.Assets;
using Nuotti.Backend.Exception;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Endpoints;

public sealed record PublishSongPackageRequest(string RevisionNote, IReadOnlyList<string> AcceptedWarningCodes);
public sealed record EvaluateSongPackageRequest(IReadOnlyList<string> AcceptedWarningCodes);

public static class SongPackageEndpoints
{
    public static void MapSongPackageEndpoints(this WebApplication app)
    {
        app.MapPut("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/package", async (
            HttpContext http, string workspaceId, string catalogEntryId, SongPackageDocument document,
            IWorkspaceAccessStore access, IPrivateAssetMetadataStore assets, ISongPackageStore packages,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null || await assets.GetEntryAsync(workspaceId, catalogEntryId, ct) is null)
                return Results.NotFound();
            if (!Bounded(document)) return ProblemResults.BadRequest("Song Package draft is too large.",
                "Hints, lyrics, routing, and text must remain within authoring limits.", ReasonCode.InvalidStateTransition);
            return Results.Ok(await packages.SaveDraftAsync(workspaceId, catalogEntryId, document,
                selected.Principal.UserId, ct));
        });

        app.MapGet("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/package", async (
            HttpContext http, string workspaceId, string catalogEntryId, IWorkspaceAccessStore access,
            ISongPackageStore packages, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var draft = await packages.GetDraftAsync(workspaceId, catalogEntryId, ct);
            return draft is null ? Results.NotFound() : Results.Ok(draft);
        });

        app.MapPost("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/package/readiness", async (
            HttpContext http, string workspaceId, string catalogEntryId, EvaluateSongPackageRequest request,
            IWorkspaceAccessStore access, ISongPackageStore packages, SongPackageReadinessEvaluator evaluator,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var draft = await packages.GetDraftAsync(workspaceId, catalogEntryId, ct);
            if (draft is null) return Results.NotFound();
            var accepted = NormalizedCodes(request.AcceptedWarningCodes);
            return Results.Ok(await evaluator.EvaluateAsync(workspaceId, draft.Document, accepted, ct));
        });

        app.MapPost("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/package/publish", async (
            HttpContext http, string workspaceId, string catalogEntryId, PublishSongPackageRequest request,
            IWorkspaceAccessStore access, ISongPackageStore packages, SongPackageReadinessEvaluator evaluator,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.RevisionNote) || request.RevisionNote.Trim().Length > 500)
                return ProblemResults.BadRequest("Revision note is required.",
                    "Describe what changed in 500 characters or fewer.", ReasonCode.InvalidStateTransition, "revisionNote");
            var draft = await packages.GetDraftAsync(workspaceId, catalogEntryId, ct);
            if (draft is null) return Results.NotFound();
            var accepted = NormalizedCodes(request.AcceptedWarningCodes);
            var readiness = await evaluator.EvaluateAsync(workspaceId, draft.Document, accepted, ct);
            var actualWarnings = readiness.Findings.Where(x => x.Severity == ReadinessSeverity.Warning)
                .Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
            if (accepted.Any(x => !actualWarnings.Contains(x)))
                return ProblemResults.BadRequest("Unknown warning override.",
                    "Only warnings currently reported by preflight can be accepted.", ReasonCode.InvalidStateTransition,
                    "acceptedWarningCodes");
            if (!readiness.CanPublish)
            {
                var unresolved = readiness.Findings.Where(x => x.Severity == ReadinessSeverity.Blocking
                    || x.Severity == ReadinessSeverity.Warning && !accepted.Contains(x.Code))
                    .Select(x => $"{x.Title}: {x.RecommendedAction}");
                return ProblemResults.UnprocessableEntity("Song Package is not Show Ready.", string.Join(" ", unresolved),
                    ReasonCode.InvalidStateTransition);
            }
            return Results.Ok(await packages.PublishAsync(workspaceId, catalogEntryId, draft.Document,
                request.RevisionNote, accepted.Order(StringComparer.Ordinal).ToArray(), selected.Principal.UserId, ct));
        });

        app.MapGet("/v1/workspaces/{workspaceId}/catalog/{catalogEntryId}/package/revisions", async (
            HttpContext http, string workspaceId, string catalogEntryId, IWorkspaceAccessStore access,
            ISongPackageStore packages, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            return Results.Ok(await packages.GetRevisionsAsync(workspaceId, catalogEntryId, ct));
        });
    }

    static HashSet<string> NormalizedCodes(IReadOnlyList<string>? values) => (values ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet(StringComparer.Ordinal);
    static bool Bounded(SongPackageDocument document) => document is not null
        && document.Playback is not null && document.Hints is { Count: <= 20 }
        && document.Hints.All(x => x is not null && x.HintId is { Length: > 0 and <= 100 }
            && (x.Text is null || x.Text.Length <= 500)
            && (x.PerformerCue is null || x.PerformerCue.Length <= 500)
            && (x.AssetRevisionId is null || x.AssetRevisionId.Length <= 100))
        && document.Playback.BackingOutputChannels is { Count: <= 8 }
        && document.Playback.ClickOutputChannels is { Count: <= 8 }
        && (document.Lyrics is null || document.Lyrics.Lrc is { Length: <= 1_000_000 });
}
