using Nuotti.Backend.Assets;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.SessionSnapshots;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Endpoints;

public static class SessionSnapshotEndpoints
{
    public static void MapSessionSnapshotEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/setlist-snapshot/preflight", async (
            HttpContext http, string workspaceId, string sessionCode, CreateSessionSetlistSnapshotRequest request,
            IWorkspaceAccessStore access, IDurableSessionCommitStore sessions, SessionSnapshotBuilder builder,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null || await sessions.LoadAsync(workspaceId, sessionCode, ct) is null) return Results.NotFound();
            var accepted = Codes(request.AcceptedWarningCodes);
            var result = await builder.BuildAsync(workspaceId, request.Songs, request.ScoringPolicy, accepted, ct);
            return Results.Ok(result.Preflight);
        });

        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/setlist-snapshot", async (
            HttpContext http, string workspaceId, string sessionCode, CreateSessionSetlistSnapshotRequest request,
            IWorkspaceAccessStore access, IDurableSessionCommitStore sessions, SessionSnapshotBuilder builder,
            ISessionSetlistSnapshotStore snapshots, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null || await sessions.LoadAsync(workspaceId, sessionCode, ct) is null) return Results.NotFound();
            if (await snapshots.GetAsync(workspaceId, sessionCode, ct) is not null)
                return ProblemResults.Conflict("Session Setlist Snapshot already exists.",
                    "A Session captures its exact show material only once.", ReasonCode.InvalidStateTransition);
            var accepted = Codes(request.AcceptedWarningCodes);
            var built = await builder.BuildAsync(workspaceId, request.Songs, request.ScoringPolicy, accepted, ct);
            var actualWarnings = built.Preflight.Findings.Where(x => x.Severity == SongPackages.ReadinessSeverity.Warning)
                .Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
            if (accepted.Any(x => !actualWarnings.Contains(x)))
                return ProblemResults.BadRequest("Unknown snapshot override.",
                    "Only current safe preflight warnings may be accepted.", ReasonCode.InvalidStateTransition);
            if (!built.Preflight.CanCreate)
                return ProblemResults.UnprocessableEntity("Session Setlist Snapshot is not ready.",
                    string.Join(" ", built.Preflight.Findings.Where(x => x.Severity != SongPackages.ReadinessSeverity.Ready
                        && (!x.CanOverride || !accepted.Contains(x.Code))).Select(x => $"{x.Title} {x.Action}")),
                    ReasonCode.InvalidStateTransition);
            try
            {
                return Results.Ok(await snapshots.CreateAsync(workspaceId, sessionCode, built.Songs,
                    built.Policy!, built.Preflight.Assets, accepted.ToArray(), selected.Principal.UserId, ct));
            }
            catch (InvalidOperationException)
            {
                return ProblemResults.Conflict("Session Setlist Snapshot already exists.",
                    "A concurrent request already captured this Session.", ReasonCode.InvalidStateTransition);
            }
        });

        app.MapGet("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/setlist-snapshot", async (
            HttpContext http, string workspaceId, string sessionCode, IWorkspaceAccessStore access,
            ISessionSetlistSnapshotStore snapshots, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var snapshot = await snapshots.GetAsync(workspaceId, sessionCode, ct);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        app.MapGet("/v1/show-agent/setlist-snapshot", async (HttpContext http, IShowAgentAccessStore agents,
            ISessionSetlistSnapshotStore snapshots, CancellationToken ct) =>
        {
            var lease = await AgentAsync(http, agents, ct);
            if (lease is null) return Results.Unauthorized();
            var snapshot = await snapshots.GetAsync(lease.WorkspaceId, lease.SessionCode, ct);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        });

        app.MapPost("/v1/show-agent/assets/{revisionId}/download", async (HttpContext http, string revisionId,
            IShowAgentAccessStore agents, ISessionSetlistSnapshotStore snapshots, IPrivateAssetMetadataStore metadata,
            IPrivateAssetObjectStore objects, CancellationToken ct) =>
        {
            var lease = await AgentAsync(http, agents, ct);
            if (lease is null) return Results.Unauthorized();
            var snapshot = await snapshots.GetAsync(lease.WorkspaceId, lease.SessionCode, ct);
            if (snapshot is null || !snapshot.Assets.Any(x => x.RevisionId == revisionId)) return Results.NotFound();
            var revision = await metadata.GetAsync(lease.WorkspaceId, revisionId, ct);
            if (revision?.Status != AssetRevisionStatus.Published || string.IsNullOrWhiteSpace(revision.Sha256)
                || revision.Provenance.RightsExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow
                || !snapshot.Assets.Any(x => x.RevisionId == revisionId
                    && x.Sha256.Equals(revision.Sha256, StringComparison.OrdinalIgnoreCase))) return Results.NotFound();
            var key = await metadata.GetObjectKeyAsync(lease.WorkspaceId, revisionId, ct);
            if (key is null) return Results.NotFound();
            try
            {
                var grant = await objects.CreateDownloadGrantAsync(key, ct);
                return Results.Ok(new PrivateAssetDownloadGrant(grant.Uri, grant.ExpiresAt));
            }
            catch (PrivateAssetGrantUnavailableException) { return Results.StatusCode(503); }
        });
    }

    static HashSet<string> Codes(IReadOnlyList<string>? values) => (values ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet(StringComparer.Ordinal);
    static async Task<ShowAgentLease?> AgentAsync(HttpContext http, IShowAgentAccessStore store, CancellationToken ct)
    {
        var value = http.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? await store.AuthenticateAsync(value[7..].Trim(), ct) : null;
    }
}
