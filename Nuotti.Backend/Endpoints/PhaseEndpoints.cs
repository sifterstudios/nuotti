using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Reducer;
namespace Nuotti.Backend.Endpoints;

internal static class PhaseEndpoints
{
    static async Task<IResult> HandlePhaseChangeAsync<T>(IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, T cmd)
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
        app.MapPost("/v1/message/phase/create-session/{session}", async (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, CreateSession cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

            var snapshot = GameReducer.Initial(session);
            stateStore.Set(session, snapshot);
            await hub.Clients.Group(session).SendAsync("GameStateChanged", snapshot);
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/start-game/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, StartGame cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/end-song/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, EndSong cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/lock-answers/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, LockAnswers cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/reveal-answer/{session}", async (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, RevealAnswer cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

            var state = stateStore.GetOrCreate(session, GameReducer.Initial);
            // Validate phase restriction explicitly for RevealAnswer
            if (!cmd.AllowedPhases.Contains(state.Phase))
            {
                return ProblemResults.Conflict(
                    title: "Invalid command phase",
                    detail: $"Command 'RevealAnswer' is not allowed in phase '{state.Phase}'.",
                    reason: ReasonCode.InvalidStateTransition);
            }

            // 1) Change phase to Reveal
            var phaseChanged = new GamePhaseChanged(state.Phase, cmd.TargetPhase)
            {
                CurrentPhase = state.Phase,
                NewPhase = cmd.TargetPhase,
                CorrelationId = cmd.CommandId,
                CausedByCommandId = cmd.CommandId,
                SessionCode = session
            };
            var (afterPhase, error) = GameReducer.Reduce(state, phaseChanged);
            if (error is not null)
            {
                return ProblemResults.Conflict(
                    title: "Reducer rejected event",
                    detail: error,
                    reason: ReasonCode.None);
            }

            // 2) Emit scoring event with the selected correct index
            var scoring = new CorrectAnswerRevealed(cmd.CorrectChoiceIndex)
            {
                CorrectChoiceIndex = cmd.CorrectChoiceIndex,
                SessionCode = session,
                CausedByCommandId = cmd.CommandId,
                CorrelationId = cmd.CommandId
            };
            var (finalState, _) = GameReducer.Reduce(afterPhase, scoring);

            stateStore.Set(session, finalState);
            await hub.Clients.Group(session).SendAsync("GameStateChanged", finalState);
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/next-round/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, NextRound cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/play-song/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, PlaySong cmd)
            => HandlePhaseChangeAsync(hub, idem, stateStore, session, cmd)).RequireCors("NuottiCors");
        
        // Non-phase-change but phase-restricted commands
        app.MapPost("/v1/message/phase/give-hint/{session}", (IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, string session, GiveHint cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return Task.FromResult<IResult>(ProblemResults.WrongRoleTriedExecutingResult(Role.Performer)); }
            if (!idem.TryRegister(session, cmd.CommandId)) { return Task.FromResult<IResult>(Results.Accepted()); }

            var state = stateStore.GetOrCreate(session, GameReducer.Initial);
            // Validate phase restriction
            if (!cmd.AllowedPhases.Contains(state.Phase))
            {
                return Task.FromResult<IResult>(ProblemResults.Conflict(
                    title: "Invalid command phase",
                    detail: $"Command 'GiveHint' is not allowed in phase '{state.Phase}'.",
                    reason: ReasonCode.InvalidStateTransition));
            }

            // Increment hint index and broadcast updated snapshot so clients can reflect the change.
            var next = state with { HintIndex = state.HintIndex + 1 };
            stateStore.Set(session, next);
            _ = hub.Clients.Group(session).SendAsync("GameStateChanged", next);
            return Task.FromResult<IResult>(Results.Accepted());
        }).RequireCors("NuottiCors");
    }


}