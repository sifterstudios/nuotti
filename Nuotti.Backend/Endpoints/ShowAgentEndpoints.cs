using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Backend.Persistence;

namespace Nuotti.Backend.Endpoints;

public sealed record PairShowAgentRequest(string Code, string Name);
public sealed record ShowAgentCredentialRequest(string Credential);
public sealed record ShowAgentStatusRequest(ShowAgentConnectionState State, string? Detail);

public static class ShowAgentEndpoints
{
    public static void MapShowAgentEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/show-agent/pairings", async (
            HttpContext http, string workspaceId, string sessionCode, IWorkspaceAccessStore workspaceStore,
            IShowAgentAccessStore agentStore, IDurableSessionCommitStore sessions, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, workspaceStore, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
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
            ShowAgentCredentialRequest request, IShowAgentAccessStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Credential))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["credential"] = ["Credential is required."] });
            var issued = await store.IssueAccessTokenAsync(request.Credential, ct);
            return issued is null ? Results.Unauthorized() : Results.Ok(new
            {
                accessToken = issued.Value.Token,
                expiresAt = issued.Value.Lease.ExpiresAt,
                workspaceId = issued.Value.Lease.WorkspaceId,
                sessionCode = issued.Value.Lease.SessionCode
            });
        });

        app.MapPut("/v1/show-agent/status", async (
            HttpContext http, ShowAgentStatusRequest request, IShowAgentAccessStore store, CancellationToken ct) =>
        {
            var lease = await AuthenticateAgentAsync(http, store, ct);
            if (lease is null) return Results.Unauthorized();
            return await store.ReportStatusAsync(lease, request.State, request.Detail, ct)
                ? Results.NoContent() : Results.Unauthorized();
        });

        app.MapGet("/v1/show-agent/commands", async (
            HttpContext http, long? after, IShowAgentAccessStore store, CancellationToken ct) =>
        {
            var lease = await AuthenticateAgentAsync(http, store, ct);
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
            var status = await agentStore.GetStatusAsync(workspaceId, sessionCode, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
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
        HttpContext http, IShowAgentAccessStore store, CancellationToken ct)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = authorization[prefix.Length..].Trim();
        return token.Length == 0 ? null : await store.AuthenticateAsync(token, ct);
    }
}
