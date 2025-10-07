using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Sessions;
namespace Nuotti.Backend.Endpoints;

internal static class PhaseEndpoints
{
    static async Task<IResult> HandlePhaseChangeAsync<T>(IHubContext<QuizHub> hub, Idempotency.IIdempotencyStore idem, IGameStateStore stateStore, string session, T cmd)
        where T : CommandBase, IPhaseChange
    {
        if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
        if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

        var state = stateStore.GetOrCreate(session, GameReducer.Initial);
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
                reason: ReasonCode.None);
        }

        stateStore.Set(session, newState);
        await hub.Clients.Group(session).SendAsync("GameStateChanged", newState);
        return Results.Accepted();
    }

    public static void MapPhaseEndpoints(this WebApplication app)
    {
        // REST endpoints for v1/Message/Phase
        app.MapPost("/v1/message/phase/create-session/{session}", async (IHubContext<QuizHub> hub, Idempotency.IIdempotencyStore idem, IGameStateStore stateStore, string session, CreateSession cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

            var snapshot = GameReducer.Initial(session);
            stateStore.Set(session, snapshot);
            await hub.Clients.Group(session).SendAsync("GameStateChanged", snapshot);
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/start-game/{session}", (IHubContext<QuizHub> hub, Idempotency.IIdempotencyStore idem, IGameStateStore stateStore, string session, StartGame cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/end-song/{session}", (IHubContext<QuizHub> hub, Idempotency.IIdempotencyStore idem, IGameStateStore stateStore, string session, EndSong cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");
    }


}