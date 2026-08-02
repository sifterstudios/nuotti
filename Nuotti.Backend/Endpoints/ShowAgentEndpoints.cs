using Nuotti.Backend.Governance;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Backend.Persistence;
using Nuotti.Contracts.V1.Governance;

namespace Nuotti.Backend.Endpoints;

public sealed record PairShowAgentRequest(string Code, string Name);
public sealed record ShowAgentCredentialRequest(string Credential);
public sealed record ShowAgentStatusRequest(ShowAgentConnectionState State, string? Detail);

public static class ShowAgentEndpoints
{
    static readonly System.Text.Json.JsonSerializerOptions LeaseJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void MapShowAgentEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/show-agent/pairings", async (
            HttpContext http, string workspaceId, string sessionCode, IWorkspaceAccessStore workspaceStore,
            IShowAgentAccessStore agentStore, IDurableSessionCommitStore sessions,
            ProductionGovernance governance, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, workspaceStore, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            if (!governance.Entitlements.IsAllowed(workspaceId, EntitlementKind.ShowAgentPairing))
                return Results.Json(new { title = "Not entitled", detail = "Show Agent pairing requires an active entitlement." },
                    statusCode: StatusCodes.Status403Forbidden);
            if (await sessions.LoadAsync(workspaceId, sessionCode, ct) is null) return Results.NotFound();
            return Results.Ok(await agentStore.IssuePairingCodeAsync(
                workspaceId, sessionCode, selected.Principal.UserId, ct));
        });

        app.MapPost("/v1/show-agent/pair", async (
            PairShowAgentRequest request, IShowAgentAccessStore store, CancellationToken ct) =>
        {
            if (request.Code?.Length != 8 || !request.Code.All(char.IsAsciiDigit)
                || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["pairing"] = ["An eight-digit code and device name are required."] });
            try
            {
                var paired = await store.PairAsync(request.Code, request.Name, ct);
                return paired is null ? Results.NotFound() : Results.Ok(paired);
            }
            catch (ShowAgentPairingThrottledException)
            {
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }
        }).RequireRateLimiting("show-agent-pairing");

        app.MapPost("/v1/show-agent/token", async (
            ShowAgentCredentialRequest request, IShowAgentAccessStore store,
            ProductionGovernance governance, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Credential))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["credential"] = ["Credential is required."] });
            var issued = await store.IssueAccessTokenAsync(request.Credential, ct);
            if (issued is null) return Results.Unauthorized();
            var signed = governance.LeaseIssuer.Issue(
                issued.Value.Lease.AgentId,
                issued.Value.Lease.WorkspaceId,
                issued.Value.Lease.SessionCode,
                issued.Value.Lease.ExpiresAt);
            return Results.Ok(new
            {
                accessToken = issued.Value.Token,
                expiresAt = issued.Value.Lease.ExpiresAt,
                workspaceId = issued.Value.Lease.WorkspaceId,
                sessionCode = issued.Value.Lease.SessionCode,
                signedLease = signed
            });
        });

        app.MapPut("/v1/show-agent/status", async (
            HttpContext http, ShowAgentStatusRequest request, IShowAgentAccessStore store,
            ProductionGovernance governance, CancellationToken ct) =>
        {
            var lease = await AuthenticateAgentAsync(http, store, governance, ct);
            if (lease is null) return Results.Unauthorized();
            return await store.ReportStatusAsync(lease, request.State, request.Detail, ct)
                ? Results.NoContent() : Results.Unauthorized();
        });

        app.MapGet("/v1/show-agent/commands", async (
            HttpContext http, long? after, IShowAgentAccessStore store,
            ProductionGovernance governance, CancellationToken ct) =>
        {
            var lease = await AuthenticateAgentAsync(http, store, governance, ct);
            if (lease is null) return Results.Unauthorized();
            var commands = await store.ReadCommandsAsync(lease, Math.Max(0, after ?? 0), ct);
            return commands is null ? Results.Unauthorized() : Results.Ok(commands);
        });

        app.MapGet("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/show-agent", async (
            HttpContext http, string workspaceId, string sessionCode, IWorkspaceAccessStore workspaceStore,
            IShowAgentAccessStore agentStore, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, workspaceStore, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var statuses = await agentStore.ListStatusesAsync(workspaceId, sessionCode, ct);
            return Results.Ok(statuses);
        });

        app.MapDelete("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/show-agent", async (
            HttpContext http, string workspaceId, string sessionCode, IWorkspaceAccessStore workspaceStore,
            IShowAgentAccessStore agentStore, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, workspaceStore, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access?.Role != WorkspaceRole.Owner) return Results.NotFound();
            return await agentStore.RevokeAsync(workspaceId, sessionCode, ct)
                ? Results.NoContent() : Results.NotFound();
        });
    }

    static async Task<ShowAgentLease?> AuthenticateAgentAsync(
        HttpContext http, IShowAgentAccessStore store, ProductionGovernance governance, CancellationToken ct)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorization[prefix.Length..].Trim();
        if (token.Length == 0) return null;
        var lease = await store.AuthenticateAsync(token, ct);
        if (lease is null) return null;

        // Optional signed-lease header: when present, integrity + expiry must verify.
        var signedHeader = http.Request.Headers["X-Nuotti-Signed-Lease"].ToString();
        if (string.IsNullOrWhiteSpace(signedHeader)) return lease;

        try
        {
            var signed = System.Text.Json.JsonSerializer.Deserialize<SignedLease>(signedHeader, LeaseJson);
            if (signed is null
                || !string.Equals(signed.AgentId, lease.AgentId, StringComparison.Ordinal)
                || !string.Equals(signed.WorkspaceId, lease.WorkspaceId, StringComparison.Ordinal)
                || !string.Equals(signed.SessionCode, lease.SessionCode, StringComparison.Ordinal)
                || !governance.LeaseIssuer.TryVerify(signed, DateTimeOffset.UtcNow, out _))
                return null;
            return lease;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
