using Nuotti.Backend.Commands;
using Nuotti.Backend.Middleware;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;

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
            HttpContext http, string workspaceId, string sessionCode, T command,
            IWorkspaceAccessStore store, ISessionCommandProcessor processor, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, store, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();

            // The command body carries who issued it, and that lands in the audit trail and in
            // every event derived from it. It is validated rather than rewritten, because a record
            // with init-only properties cannot be corrected generically - and a caller that
            // disagrees with the server about who it is should be told, not quietly relabelled.
            if (!string.Equals(command.SessionCode, sessionCode, StringComparison.Ordinal))
                return Mismatch("sessionCode", "The command's session must match the route.");
            if (command.IssuedByRole != Role.Performer)
                return Mismatch("issuedByRole", "Workspace commands are issued as the Performer.");
            if (!string.Equals(command.IssuedById, selected.Principal.UserId, StringComparison.Ordinal))
                return Mismatch("issuedById", "The command must be issued by the signed-in member sending it.");

            var result = await processor.ApplyAsync(sessionCode,
                Actor.Verified(Role.Performer, selected.Principal.UserId), command,
                CorrelationIdMiddleware.GetCorrelationId(http), ct, workspaceId);
            return result.ToHttpResult();
        }).RequireCors("NuottiCors");
    }

    static IResult Mismatch(string field, string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [message] });
}
