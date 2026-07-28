using Nuotti.Backend.Commands;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Middleware;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
namespace Nuotti.Backend.Endpoints;

/// <summary>
/// HTTP adapter over <see cref="ISessionCommandProcessor"/>. Every endpoint hands the processor a
/// Command and translates the result; none of them touch state, the reducer, or SignalR.
/// </summary>
internal static class PhaseEndpoints
{
    public static void MapPhaseEndpoints(this WebApplication app)
    {
        app.MapPhaseCommand<CreateSession>("create-session");
        app.MapPhaseCommand<StartGame>("start-game");
        app.MapPhaseCommand<OpenAnswers>("open-answers");
        app.MapPhaseCommand<EndSong>("end-song");
        app.MapPhaseCommand<LockAnswers>("lock-answers");
        app.MapPhaseCommand<RevealAnswer>("reveal-answer");
        app.MapPhaseCommand<NextRound>("next-round");
        app.MapPhaseCommand<PlaySong>("play-song");
        app.MapPhaseCommand<GiveHint>("give-hint");
        app.MapPhaseCommand<EndGame>("end-game");
    }

    static void MapPhaseCommand<T>(this WebApplication app, string route) where T : CommandBase
    {
        app.MapPost($"/v1/message/phase/{route}/{{session}}",
                async (HttpContext http, ISessionCommandProcessor processor, string session, T cmd) =>
                {
                    var result = await processor.ApplyAsync(
                        session,
                        Actor.Claimed(cmd),
                        cmd,
                        CorrelationIdMiddleware.GetCorrelationId(http),
                        http.RequestAborted);

                    return result.ToHttpResult();
                })
            .RequireCors("NuottiCors");
    }

    /// <summary>
    /// Maps an <see cref="Outcome"/> onto an HTTP response. A duplicate is reported as accepted:
    /// the caller's intent has been satisfied, just not twice.
    /// </summary>
    internal static IResult ToHttpResult(this CommandResult result) => result.Outcome switch
    {
        Outcome.Applied or Outcome.Duplicate => Results.Accepted(),
        _ => ProblemResults.From(result.Problem!)
    };
}
