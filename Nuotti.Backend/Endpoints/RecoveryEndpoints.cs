using System.Text.Json;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;

namespace Nuotti.Backend.Endpoints;

public sealed record ReplayMessage(SessionSequence Sequence, string MessageType, JsonElement Payload);
public sealed record SessionRecovery(
    SessionSnapshot<GameStateSnapshot> Snapshot,
    IReadOnlyList<ReplayMessage> Events);

public static class RecoveryEndpoints
{
    public static void MapRecoveryEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/workspaces/{workspaceId}/sessions/{sessionCode}/recovery", async (
            HttpContext http,
            string workspaceId,
            string sessionCode,
            IWorkspaceAccessStore accessStore,
            IDurableSessionCommitStore store,
            CancellationToken cancellationToken) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(
                http, accessStore, workspaceId, cancellationToken);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();

            var record = await store.LoadAsync(workspaceId, sessionCode, cancellationToken);
            if (record is null) return Results.NotFound();

            // This endpoint returns the latest compatible snapshot. Replay therefore starts
            // strictly after that snapshot cursor; it never mixes already-reduced history into it.
            var events = await store.ReadAfterAsync(workspaceId, sessionCode, record.LastSequence, cancellationToken);
            var recovery = new SessionRecovery(
                new SessionSnapshot<GameStateSnapshot>(
                    SessionProtocolVersion.Current,
                    SessionProtocolVersion.Current,
                    workspaceId,
                    sessionCode,
                    record.LastSequence,
                    record.ControlGeneration,
                    record.Snapshot),
                events.Select(message => new ReplayMessage(
                    message.Sequence,
                    message.MessageType,
                    JsonSerializer.Deserialize<JsonElement>(message.Payload, ContractsJson.RestOptions)))
                .ToArray());
            return Results.Ok(recovery);
        });
    }
}
