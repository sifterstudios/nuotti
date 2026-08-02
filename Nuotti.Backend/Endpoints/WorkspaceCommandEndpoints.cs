using Nuotti.Backend.Commands;
using Nuotti.Backend.Middleware;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// The Performer's control surface: every game command, scoped to a workspace and a session.
/// </summary>
/// <remarks>
/// These are the deployed counterpart to <c>/v1/message/phase/*</c>, which is local-only because it
/// takes the issuing role from the request body and believes it. Here the caller is a signed-in
/// member with that workspace selected, so the command is applied on behalf of a
/// <see cref="Actor.Verified"/> Performer. Without this, a deployed Nuotti could create a session
/// and start a game and then had no way to run one.
/// </remarks>
internal static class WorkspaceCommandEndpoints
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapWorkspaceCommandEndpoints(this WebApplication app)
    {
        app.MapWorkspaceCommand<StartGame>("start-game");
        app.MapWorkspaceCommand<NextRound>("next-round");
        app.MapWorkspaceCommand<PlaySong>("play-song");
        app.MapWorkspaceCommand<OpenAnswers>("open-answers");
        app.MapWorkspaceCommand<GiveHint>("give-hint");
        app.MapWorkspaceCommand<LockAnswers>("lock-answers");
        app.MapWorkspaceCommand<RevealAnswer>("reveal-answer");
        app.MapWorkspaceCommand<EndSong>("end-song");
        app.MapWorkspaceCommand<EndGame>("end-game");
        app.MapWorkspaceCommand<PreparePlayback>("prepare-playback");
        app.MapWorkspaceCommand<StartPlayback>("start-playback");

        // Relays. These reach the venue rig and the crowd through the same event bus as the phase
        // commands, so they belong on the same authorized surface rather than on /api.
        app.MapWorkspaceCommand<QuestionPushed>("push-question");
        app.MapWorkspaceCommand<PlayTrack>("play");
        app.MapWorkspaceCommand<StopTrack>("stop");
        app.MapWorkspaceCommand<UpdateCatalog>("update-catalog");
    }

    static void MapWorkspaceCommand<T>(this WebApplication app, string route) where T : CommandBase
    {
        app.MapPost($"/v1/workspaces/{{workspaceId}}/sessions/{{sessionCode}}/commands/{route}", async (
            HttpContext http, string workspaceId, string sessionCode, JsonElement body,
            IWorkspaceAccessStore store, ISessionCommandProcessor processor, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();

            T? command;
            try
            {
                command = Stamp(body, sessionCode, selected.Principal.UserId).Deserialize<T>(Json);
            }
            catch (JsonException exception)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["command"] = [exception.Message] });
            }
            if (command is null) return Results.BadRequest();

            var result = await processor.ApplyAsync(sessionCode,
                Actor.Verified(Role.Performer, selected.Principal.UserId), command,
                CorrelationIdMiddleware.GetCorrelationId(http), ct, workspaceId);
            return result.ToHttpResult();
        }).RequireCors("NuottiCors");
    }

    /// <summary>
    /// Writes who issued this command and which session it belongs to, over whatever the caller
    /// said.
    /// </summary>
    /// <remarks>
    /// These three fields land in the audit trail and in every event derived from the command, so
    /// they are stamped rather than validated: a client that gets them wrong is corrected instead
    /// of refused, and a client that lies about them cannot. Commands are records with init-only
    /// properties, so this happens on the JSON on the way in - there is nowhere later to do it.
    /// </remarks>
    static JsonNode Stamp(JsonElement body, string sessionCode, string userId)
    {
        var node = JsonNode.Parse(body.GetRawText()) as JsonObject
            ?? throw new JsonException("A command must be a JSON object.");
        node["sessionCode"] = sessionCode;
        node["issuedByRole"] = Role.Performer.ToString();
        node["issuedById"] = userId;
        return node;
    }
}
