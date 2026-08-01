using Microsoft.AspNetCore.Mvc;
using Nuotti.Backend.Audit;
using Nuotti.Backend.Governance;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Governance;

namespace Nuotti.Backend.Endpoints;

internal static class GovernanceEndpoints
{
    public static void MapGovernanceEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/workspaces/{workspaceId}/diagnostics/verbose", async (
            HttpContext http,
            string workspaceId,
            ProductionGovernance governance,
            IWorkspaceAccessStore access,
            AuditLogService audit,
            [FromBody] ElevateVerboseRequest? body,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (selected.Access.Role != WorkspaceRole.Owner)
                return Results.Forbid();

            governance.Entitlements.Grant(workspaceId, EntitlementKind.DiagnosticsExport);
            var ttl = TimeSpan.FromMinutes(Math.Clamp(body?.Minutes ?? 15, 1, 120));
            var level = body?.Verbose == true ? DiagnosticVerbosity.Verbose : DiagnosticVerbosity.Debug;
            var now = DateTimeOffset.UtcNow;
            var scopeId = governance.VerboseCapture.Elevate(level, ttl, now, body?.ScopeId);
            governance.ApplyVerboseLevel(now);
            audit.LogGovernanceAction(
                "diagnostics.verbose.elevate",
                workspaceId,
                selected.Principal.UserId,
                $"scope={scopeId};level={level};ttlMinutes={ttl.TotalMinutes}");
            return Results.Ok(new
            {
                scopeId,
                level = level.ToString(),
                expiresAt = now.Add(ttl)
            });
        });

        app.MapPost("/v1/workspaces/{workspaceId}/diagnostics/export", async (
            HttpContext http,
            string workspaceId,
            ProductionGovernance governance,
            IWorkspaceAccessStore access,
            Nuotti.Backend.Diagnostics.DiagnosticsBundleService bundles,
            AuditLogService audit,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (!governance.Entitlements.IsAllowed(workspaceId, EntitlementKind.DiagnosticsExport))
                return Results.Json(new { title = "Not entitled", detail = "Diagnostics export requires an active entitlement." },
                    statusCode: StatusCodes.Status403Forbidden);

            var evidence = new BoundedSupportEvidence();
            evidence.TryAdd("workspace", SafeTelemetryIdentifiers.CorrelateWorkspace(workspaceId));
            evidence.TryAdd("verbose-scope", governance.VerboseCapture.ActiveScopeId(DateTimeOffset.UtcNow) ?? "none");
            evidence.TryAdd("config", string.Join('\n',
                bundles.RedactConfiguration().Select(kv => $"{kv.Key}={kv.Value}")));

            var path = await bundles.CreateBoundedBundleAsync(evidence, workspaceId, ct);
            try
            {
                var bytes = await File.ReadAllBytesAsync(path, ct);
                audit.LogGovernanceAction(
                    "diagnostics.export",
                    workspaceId,
                    selected.Principal.UserId,
                    $"bytes={bytes.Length};truncated={evidence.Truncated}");
                return Results.File(bytes, "application/zip", Path.GetFileName(path));
            }
            finally
            {
                try { File.Delete(path); } catch { /* best-effort temp cleanup */ }
            }
        });

        app.MapPost("/v1/workspaces/{workspaceId}/entitlements/{kind}", async (
            HttpContext http,
            string workspaceId,
            string kind,
            ProductionGovernance governance,
            IWorkspaceAccessStore access,
            AuditLogService audit,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (selected.Access.Role != WorkspaceRole.Owner)
                return Results.Forbid();
            if (!Enum.TryParse<EntitlementKind>(kind, ignoreCase: true, out var entitlement))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["kind"] = ["Unknown entitlement kind."]
                });

            governance.Entitlements.Grant(workspaceId, entitlement);
            audit.LogGovernanceAction("entitlement.grant", workspaceId, selected.Principal.UserId, entitlement.ToString());
            return Results.Ok(new { workspaceId, kind = entitlement.ToString(), granted = true });
        });

        app.MapPost("/v1/workspaces/{workspaceId}/assets/{revisionId}/takedown", async (
            HttpContext http,
            string workspaceId,
            string revisionId,
            ProductionGovernance governance,
            IWorkspaceAccessStore access,
            AuditLogService audit,
            [FromBody] TakedownRequest? body,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (selected.Access.Role != WorkspaceRole.Owner)
                return Results.Forbid();

            var opened = governance.Takedowns.Open(workspaceId, revisionId, body?.Reason ?? "takedown", DateTimeOffset.UtcNow);
            var enforced = governance.Takedowns.Enforce(opened.CaseId, DateTimeOffset.UtcNow);
            audit.LogGovernanceAction(
                "asset.takedown.enforce",
                workspaceId,
                selected.Principal.UserId,
                $"revision={revisionId};case={enforced.CaseId}");
            return Results.Ok(enforced);
        });

        app.MapGet("/v1/workspaces/{workspaceId}/assets/{revisionId}/takedown-status", (
            string workspaceId,
            string revisionId,
            ProductionGovernance governance) =>
            Results.Ok(new { blocked = governance.Takedowns.IsBlocked(workspaceId, revisionId) }));
    }

    public sealed record ElevateVerboseRequest(int? Minutes, bool? Verbose, string? ScopeId);
    public sealed record TakedownRequest(string? Reason);
}
