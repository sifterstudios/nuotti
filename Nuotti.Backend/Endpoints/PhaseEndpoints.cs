using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Middleware;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Telemetry;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Reducer;
using Serilog.Context;
namespace Nuotti.Backend.Endpoints;

internal static class PhaseEndpoints
{
    static async Task<IResult> HandlePhaseChangeAsync<T>(HttpContext? httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics? metrics, Nuotti.Backend.Audit.AuditLogService? auditService, string session, T cmd)
        where T : CommandBase, IPhaseChange
    {
        // Create OpenTelemetry span for command handling
        using var activity = BackendActivitySource.StartCommandHandling(typeof(T).Name, session, cmd.CommandId);
        activity?.SetTag("command.target_phase", cmd.TargetPhase.ToString());

        if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
        if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

        // Record command received for latency tracking
        metrics?.RecordCommandReceived(cmd.CommandId);

        var state = stateStore.GetOrCreate(session, GameReducer.Initial);
        if (!cmd.IsPhaseChangeAllowed(state.Phase))
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", "Invalid state transition");
            return ProblemResults.Conflict(
                title: "Invalid state transition",
                detail: $"Cannot change phase from {state.Phase} to {cmd.TargetPhase}.",
                reason: ReasonCode.InvalidStateTransition);
        }

        // Use correlation ID from HTTP context if available, otherwise use CommandId
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext) ?? cmd.CommandId;
        activity?.SetTag("correlation.id", correlationId.ToString());

        var ev = new GamePhaseChanged(state.Phase, cmd.TargetPhase)
        {
            CurrentPhase = state.Phase,
            NewPhase = cmd.TargetPhase,
            CorrelationId = correlationId,
            CausedByCommandId = cmd.CommandId,
            SessionCode = session
        };
        var (newState, error) = GameReducer.Reduce(state, ev);
        if (error is not null)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", error);
            return ProblemResults.Conflict(
                title: "Reducer rejected event",
                detail: error,
                reason: ReasonCode.None);
        }

        stateStore.Set(session, newState);
        await hub.Clients.Group(session).SendAsync("GameStateChanged", newState);
        
        // Record command applied (event broadcast = command applied)
        metrics?.RecordCommandApplied(cmd.CommandId);
        activity?.SetTag("command.applied", true);
        
        // Log audit entry for command applied
        auditService?.LogCommandApplied(cmd, $"Phase={newState.Phase}, SongIndex={newState.SongIndex}, TotalAnswers={newState.TotalAnswers}, Players={newState.Scores.Count}");
        
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

        app.MapPost("/v1/message/phase/start-game/{session}", (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, Nuotti.Backend.Audit.AuditLogService auditService, string session, StartGame cmd)
            => HandlePhaseChangeAsync(httpContext, hub, idem, stateStore, metrics, auditService, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/end-song/{session}", (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, Nuotti.Backend.Audit.AuditLogService auditService, string session, EndSong cmd)
            => HandlePhaseChangeAsync(httpContext, hub, idem, stateStore, metrics, auditService, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/lock-answers/{session}", (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, Nuotti.Backend.Audit.AuditLogService auditService, string session, LockAnswers cmd)
            => HandlePhaseChangeAsync(httpContext, hub, idem, stateStore, metrics, auditService, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/reveal-answer/{session}", async (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, string session, RevealAnswer cmd) =>
        {
            if (cmd.IssuedByRole != Role.Performer) { return ProblemResults.WrongRoleTriedExecutingResult(Role.Performer); }
            if (!idem.TryRegister(session, cmd.CommandId)) { return Results.Accepted(); }

            // Record command received for latency tracking
            metrics?.RecordCommandReceived(cmd.CommandId);

            var state = stateStore.GetOrCreate(session, GameReducer.Initial);
            // Validate phase restriction explicitly for RevealAnswer
            if (!cmd.AllowedPhases.Contains(state.Phase))
            {
                return ProblemResults.Conflict(
                    title: "Invalid command phase",
                    detail: $"Command 'RevealAnswer' is not allowed in phase '{state.Phase}'.",
                    reason: ReasonCode.InvalidStateTransition);
            }

            // Use correlation ID from HTTP context if available, otherwise use CommandId
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext) ?? cmd.CommandId;

            // 1) Change phase to Reveal
            var phaseChanged = new GamePhaseChanged(state.Phase, cmd.TargetPhase)
            {
                CurrentPhase = state.Phase,
                NewPhase = cmd.TargetPhase,
                CorrelationId = correlationId,
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
                CorrelationId = correlationId
            };
            var (finalState, _) = GameReducer.Reduce(afterPhase, scoring);

            stateStore.Set(session, finalState);
            await hub.Clients.Group(session).SendAsync("GameStateChanged", finalState);
            
            // Record command applied (event broadcast = command applied)
            metrics?.RecordCommandApplied(cmd.CommandId);
            
            return Results.Accepted();
        }).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/next-round/{session}", (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, Nuotti.Backend.Audit.AuditLogService auditService, string session, NextRound cmd)
            => HandlePhaseChangeAsync(httpContext, hub, idem, stateStore, metrics, auditService, session, cmd)).RequireCors("NuottiCors");

        app.MapPost("/v1/message/phase/play-song/{session}", (HttpContext httpContext, IHubContext<QuizHub> hub, IIdempotencyStore idem, IGameStateStore stateStore, BackendMetrics metrics, Nuotti.Backend.Audit.AuditLogService auditService, string session, PlaySong cmd)
            => HandlePhaseChangeAsync(httpContext, hub, idem, stateStore, metrics, auditService, session, cmd)).RequireCors("NuottiCors");
        
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