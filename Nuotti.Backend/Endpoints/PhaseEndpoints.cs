using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;

namespace Microsoft.AspNetCore.Builder;

internal static class PhaseEndpoints
{
    static readonly ConcurrentDictionary<string, GameStateSnapshot> gameStates = new ConcurrentDictionary<string, GameStateSnapshot>();

    static GameStateSnapshot GetState(string session)
        => gameStates.GetOrAdd(session, s => GameReducer.Initial(s));

    static async Task<IResult> HandlePhaseChangeAsync<T>(IHubContext<QuizHub> hub, Nuotti.Backend.Idempotency.IIdempotencyStore idem, string session, T cmd)
        where T : CommandBase, IPhaseChange
    {
        // Idempotency: short-circuit duplicates
        if (!idem.TryRegister(session, cmd.CommandId))
        {
            return Results.Accepted();
        }

        var state = GetState(session);
        if (!cmd.IsPhaseChangeAllowed(state.Phase))
        {
            return ProblemResults.Conflict(
                title: "Invalid state transition",
                detail: $"Cannot change phase from {state.Phase} to {cmd.TargetPhase}.",
                reason: ReasonCode.InvalidStateTransition);
        }

        var ev = new GamePhaseChanged(state.Phase, cmd.TargetPhase)
        {
            CurrentPhase = state.Phase,
            NewPhase = cmd.TargetPhase,
            CorrelationId = cmd.CommandId,
            CausedByCommandId = cmd.CommandId,
            SessionCode = session
        };
        var (newState, error) = GameReducer.Reduce(state, ev);
        if (error is not null)
        {
            return ProblemResults.Conflict(
                title: "Reducer rejected event",
                detail: error,
                reason: ReasonCode.InvalidStateTransition);
        }

        gameStates[session] = newState;
        await hub.Clients.Group(session).SendAsync("GameStateChanged", newState);
        return Results.Accepted();
    }

    public static void MapPhaseEndpoints(this WebApplication app)
    {
        // REST endpoints for v1/Message/Phase
        app.MapPost("/v1/message/phase/create-session/{session}", async (IHubContext<QuizHub> hub, Nuotti.Backend.Idempotency.IIdempotencyStore idem, string session, CreateSession cmd) =>
        {
            if (!idem.TryRegister(session, cmd.CommandId))
            {
                return Results.Accepted();
            }
            var snapshot = GameReducer.Initial(session);
            gameStates[session] = snapshot;
            await hub.Clients.Group(session).SendAsync("GameStateChanged", snapshot);
            return Results.Accepted();
        }).RequireCors("AllowAll");

        app.MapPost("/v1/message/phase/start-game/{session}", (IHubContext<QuizHub> hub, Nuotti.Backend.Idempotency.IIdempotencyStore idem, string session, StartGame cmd)
            => HandlePhaseChangeAsync(hub, idem, session, cmd)).RequireCors("AllowAll");

        app.MapPost("/v1/message/phase/end-song/{session}", (IHubContext<QuizHub> hub, Nuotti.Backend.Idempotency.IIdempotencyStore idem, string session, EndSong cmd)
            => HandlePhaseChangeAsync(hub, idem, session, cmd)).RequireCors("AllowAll");
    }
}
