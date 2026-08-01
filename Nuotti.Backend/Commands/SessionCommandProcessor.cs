using Nuotti.Backend.Audit;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.Telemetry;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
using Nuotti.Contracts.V1.Protocol;
using System.ComponentModel.DataAnnotations;
using PhaseEnum = Nuotti.Contracts.V1.Enum.Phase;
namespace Nuotti.Backend.Commands;

/// <inheritdoc />
public sealed class SessionCommandProcessor(
    IGameStateStore store,
    IIdempotencyStore idempotency,
    IEventBus bus,
    ILogger<SessionCommandProcessor> logger,
    BackendMetrics? metrics = null,
    AuditLogService? audit = null,
    IDurableSessionCommitStore? durable = null,
    DurableOutboxDispatcher? outbox = null) : ISessionCommandProcessor
{
    /// <summary>
    /// What a Command does. Events are reduced in order; the reducer ignores types it does not know,
    /// which is how relay Commands pass through without touching state.
    /// </summary>
    /// <param name="Events">Events (or, for relay Commands, the Command itself) to publish.</param>
    /// <param name="BroadcastSnapshot">
    /// Whether to publish a GameStateChanged carrying the resulting snapshot. False for
    /// SubmitAnswer: a full snapshot per answer would be quadratic in audience size, so clients
    /// apply the same reducer to the AnswerSubmitted event instead.
    /// </param>
    /// <param name="CheckIdempotency">
    /// False for relay Commands, which stay at-least-once — see docs/adr/0002.
    /// </param>
    sealed record Effects(
        IReadOnlyList<object> Events,
        bool BroadcastSnapshot = true,
        bool CheckIdempotency = true);

    sealed class StaleCommitException : System.Exception { }

    public async Task<CommandResult> ApplyAsync(
        string session,
        Actor actor,
        CommandBase command,
        Guid? correlationId = null,
        CancellationToken ct = default,
        string workspaceId = "legacy")
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await ApplyAttemptAsync(session, actor, command, correlationId, ct, workspaceId);
            }
            catch (StaleCommitException)
            {
                if (attempt == maxAttempts) break;
                await Task.Yield();
            }
        }

        return CommandResult.Rejected(NuottiProblem.Conflict(
            "Session is busy",
            "The Session changed repeatedly while this command was being applied. Retry the command.",
            ReasonCode.InvalidStateTransition,
            correlationId: correlationId ?? command.CommandId));
    }

    async Task<CommandResult> ApplyAttemptAsync(
        string session,
        Actor actor,
        CommandBase command,
        Guid? correlationId,
        CancellationToken ct,
        string workspaceId)
    {
        using var activity = BackendActivitySource.StartCommandHandling(
            command.GetType().Name, session, command.CommandId);
        activity?.SetTag("actor.role", actor.Role.ToString());
        activity?.SetTag("actor.server_verified", actor.ServerVerified);

        var correlation = correlationId ?? command.CommandId;
        activity?.SetTag("correlation.id", correlation.ToString());

        var requiredRole = RequiredRole(command);
        if (actor.Role != requiredRole)
        {
            // 403 with this exact title/detail/field is the established REST contract for a wrong
            // role; it was ProblemResults.WrongRoleTriedExecutingResult before this module existed.
            return Reject(activity, new NuottiProblem(
                Title: "Unauthorized Role",
                Status: 403,
                Detail: $"Only {requiredRole} may execute this command.",
                Reason: ReasonCode.UnauthorizedRole,
                Field: "issuedByRole",
                CorrelationId: correlation));
        }

        if (durable is not null)
        {
            var prior = await durable.FindOutcomeAsync(workspaceId, session, command.CommandId, ct);
            if (prior is not null) return prior;
        }

        // create-session is guarded on existence rather than on phase. A fresh session reads as
        // Lobby (GameReducer.Initial), so guarding it against its declared AllowedPhases of [Idle]
        // would reject every create. Guarding on existence is what actually matters: re-sending
        // create mid-game used to silently reset scores and tallies.
        if (command is CreateSession)
        {
            if (durable is null && !idempotency.TryRegister(session, command.CommandId)) return CommandResult.Duplicate();
            metrics?.RecordCommandReceived(command.CommandId);

            var durableExisting = durable is null ? null : await durable.LoadAsync(workspaceId, session, ct);
            if (durableExisting is not null || store.TryGet(session, out _))
            {
                return Reject(activity, NuottiProblem.Conflict(
                    title: "Session already exists",
                    detail: $"Session '{session}' has already been created.",
                    reason: ReasonCode.InvalidStateTransition,
                    correlationId: correlation));
            }

            var seeded = GameReducer.Initial(session);
            var createdEvent = StateChanged(session, seeded, command, correlation);
            if (durable is not null)
            {
                var commit = await durable.CommitAsync(
                    workspaceId, session, command.CommandId, SessionSequence.None, seeded, [createdEvent],
                    DurableCommitPrecondition.SessionMustNotExist, ct);
                if (commit.WasDuplicate) return commit.Result;
                if (commit.Result.Outcome == Outcome.Rejected) return commit.Result;
                store.Set(session, seeded);
                if (outbox is not null) await outbox.DispatchPendingAsync(cancellationToken: ct);
            }
            else
            {
                store.Set(session, seeded);
                await SessionMessagePublisher.PublishAsync(bus, createdEvent, ct, workspaceId);
            }
            Complete(activity, command, seeded);
            return CommandResult.Applied(seeded);
        }

        var persisted = durable is null ? null : await durable.LoadAsync(workspaceId, session, ct);
        var state = persisted?.Snapshot ?? store.GetOrCreate(session, GameReducer.Initial);
        if (persisted is not null) store.Set(session, state);

        var effects = EffectsFor(command, state, actor, session, correlation, out var rejection);
        if (rejection is not null) return Reject(activity, rejection);

        if (durable is null && effects.CheckIdempotency && !idempotency.TryRegister(session, command.CommandId))
        {
            return CommandResult.Duplicate();
        }
        metrics?.RecordCommandReceived(command.CommandId);

        var guardProblem = Guard(state.Phase, command, correlation);
        if (guardProblem is not null) return Reject(activity, guardProblem);

        var next = state;
        foreach (var evt in effects.Events)
        {
            var (reduced, error) = GameReducer.Reduce(next, evt);
            if (error is not null)
            {
                return Reject(activity, NuottiProblem.Conflict(
                    title: "Reducer rejected event",
                    detail: error,
                    reason: ReasonCode.None,
                    correlationId: correlation));
            }
            next = reduced;
        }

        var stateChanged = !ReferenceEquals(next, state);
        var publications = effects.Events.ToList();
        if (effects.BroadcastSnapshot && stateChanged)
            publications.Add(StateChanged(session, next, command, correlation));

        if (durable is not null && effects.CheckIdempotency)
        {
            var expectedSequence = persisted?.LastSequence ?? SessionSequence.None;
            // Relays (PlayTrack, PreparePlayback, …) are at-least-once and not outbox-durable —
            // commit only durable events, then publish relays live so Show Agent still receives them.
            var durablePublications = publications.Where(SessionMessagePublisher.IsDurable).ToList();
            var liveRelays = publications.Where(p => !SessionMessagePublisher.IsDurable(p)).ToList();
            var commit = await durable.CommitAsync(
                workspaceId, session, command.CommandId, expectedSequence, next, durablePublications,
                cancellationToken: ct);
            if (commit.WasDuplicate) return commit.Result;
            if (commit.WasStale)
                throw new StaleCommitException();
            store.Set(session, next);
            if (outbox is not null) await outbox.DispatchPendingAsync(cancellationToken: ct);
            foreach (var relay in liveRelays) await PublishAsync(relay, ct, workspaceId);
        }
        else
        {
            if (stateChanged) store.Set(session, next);
            foreach (var publication in publications) await PublishAsync(publication, ct, workspaceId);
            // At-least-once relays that also change state (QuestionPushed → QuestionOffered) must
            // still advance the Workspace durable snapshot; otherwise the next phase command
            // reloads a Choices-less record and wipes memory. Use a fresh CommandId so retries
            // remain at-least-once on the wire (docs/adr/0002).
            if (durable is not null && stateChanged && workspaceId != "legacy")
            {
                var durableOnly = publications.Where(SessionMessagePublisher.IsDurable).ToList();
                if (durableOnly.Count > 0)
                {
                    var latest = await durable.LoadAsync(workspaceId, session, ct);
                    var expectedSequence = latest?.LastSequence ?? SessionSequence.None;
                    await durable.CommitAsync(
                        workspaceId, session, Guid.NewGuid(), expectedSequence, next, durableOnly,
                        cancellationToken: ct);
                }
            }
        }

        Complete(activity, command, stateChanged ? next : null);
        return CommandResult.Applied(stateChanged ? next : null);
    }

    /// <summary>
    /// The Command-to-effects mapping. Every special case lives here rather than in a caller.
    /// </summary>
    Effects EffectsFor(
        CommandBase command,
        GameStateSnapshot state,
        Actor actor,
        string session,
        Guid correlation,
        out NuottiProblem? rejection)
    {
        rejection = null;

        switch (command)
        {
            case RevealAnswer reveal:
                // Two events: the phase moves, then scores are awarded.
                return new Effects([
                    Phase(state.Phase, reveal.TargetPhase, session, command, correlation),
                    new CorrectAnswerRevealed(reveal.CorrectChoiceIndex)
                    {
                        CorrectChoiceIndex = reveal.CorrectChoiceIndex,
                        SessionCode = session,
                        CausedByCommandId = command.CommandId,
                        CorrelationId = correlation
                    }
                ]);

            // StartPlayback must be matched before IPhaseChange: it both moves Reveal→Play and
            // relays a cache-resolved PlayTrack so the Show Agent can start backing/click.
            case StartPlayback start:
                if (string.IsNullOrWhiteSpace(start.AssetRevisionId))
                {
                    rejection = NuottiProblem.UnprocessableEntity(
                        title: "Invalid Start",
                        detail: "AssetRevisionId from the Session Setlist Snapshot is required.",
                        field: "assetRevisionId",
                        correlationId: correlation);
                    return new Effects([]);
                }
                return new Effects([
                    Phase(state.Phase, start.TargetPhase, session, command, correlation),
                    new PlayTrack(start.AssetRevisionId)
                    {
                        AssetRevisionId = start.AssetRevisionId,
                        SessionCode = session,
                        IssuedByRole = command.IssuedByRole,
                        IssuedById = command.IssuedById,
                        CommandId = command.CommandId
                    }
                ]);

            case PreparePlayback prepare:
                return new Effects([prepare], BroadcastSnapshot: false, CheckIdempotency: false);

            case IPhaseChange phaseChange:
                return new Effects([
                    Phase(state.Phase, phaseChange.TargetPhase, session, command, correlation)
                ]);

            case GiveHint:
                // Was `state with { HintIndex = state.HintIndex + 1 }` inline in the endpoint,
                // outside the reducer and outside audit and metrics.
                return new Effects([
                    new HintGiven(state.HintIndex + 1)
                    {
                        SessionCode = session,
                        CausedByCommandId = command.CommandId,
                        CorrelationId = correlation
                    }
                ]);

            case SubmitAnswer submit:
                return new Effects(
                    [
                        new AnswerSubmitted(actor.Id, submit.ChoiceIndex)
                        {
                            AudienceId = actor.Id,
                            ChoiceIndex = submit.ChoiceIndex,
                            SessionCode = session,
                            CausedByCommandId = command.CommandId,
                            CorrelationId = correlation
                        }
                    ],
                    BroadcastSnapshot: false);

            case UpdateCatalog updateCatalog:
                var catalog = BuildCatalog(updateCatalog.Manifest, correlation, out rejection);
                if (rejection is not null) return new Effects([]);
                return new Effects([
                    new CatalogUpdated(catalog)
                    {
                        SessionCode = session,
                        CausedByCommandId = command.CommandId,
                        CorrelationId = correlation
                    }
                ]);

            // QuestionPushed is still relayed untouched for the wire, but it now also produces a
            // state event: the choices have to reach GameStateSnapshot or the reducer cannot
            // bounds-check an answer. Idempotency stays off per docs/adr/0002 — re-offering the
            // same choices is idempotent in effect.
            case QuestionPushed pushed:
                // Options carries no data annotations, and this relay skips idempotency by
                // design, so a request that omits "options" (or sends an empty array) reaches
                // here with Options null or empty despite its compile-time type of string[] —
                // System.Text.Json does not enforce non-nullable annotations at runtime. Left
                // unchecked, GameReducer's SequenceEqual(offered.Choices) throws
                // ArgumentNullException, which ProblemHandlingMiddleware turns into a 500-free but
                // useless response leaking a LINQ parameter name ("second") into the contract.
                if (pushed.Options is null || pushed.Options.Length == 0)
                {
                    rejection = NuottiProblem.UnprocessableEntity(
                        title: "Invalid question",
                        detail: "At least one option is required.",
                        field: "options",
                        correlationId: correlation);
                    return new Effects([]);
                }
                return new Effects(
                    [pushed, new QuestionOffered(pushed.Text, pushed.Options)
                    {
                        Text = pushed.Text,
                        Choices = pushed.Options,
                        SessionCode = session,
                        CausedByCommandId = command.CommandId,
                        CorrelationId = correlation
                    }],
                    BroadcastSnapshot: true,
                    CheckIdempotency: false);

            // Relay Commands: forwarded to clients untouched, no state change, no idempotency
            // (docs/adr/0002). The reducer ignores them, so no snapshot is broadcast either.
            case PlayTrack:
            case StopTrack:
                return new Effects([command], BroadcastSnapshot: false, CheckIdempotency: false);

            default:
                logger.LogWarning("No effects mapped for command {Command}", command.GetType().Name);
                return new Effects([]);
        }
    }

    static GamePhaseChanged Phase(
        PhaseEnum current, PhaseEnum target, string session, CommandBase command, Guid correlation)
        => new(current, target)
        {
            CurrentPhase = current,
            NewPhase = target,
            SessionCode = session,
            CausedByCommandId = command.CommandId,
            CorrelationId = correlation
        };

    static Role RequiredRole(CommandBase command)
        => command is SubmitAnswer ? Role.Audience : Role.Performer;

    /// <summary>
    /// The phase guard, using the declarations the Commands already carry. PhaseGuard's throwing
    /// helpers are not used: an illegal transition is an expected answer here, not an exception.
    /// </summary>
    static NuottiProblem? Guard(PhaseEnum current, CommandBase command, Guid correlation)
    {
        if (command is IPhaseRestricted restricted && !restricted.AllowedPhases.Contains(current))
        {
            return NuottiProblem.Conflict(
                title: "Invalid command phase",
                detail: $"Command '{command.GetType().Name}' is not allowed in phase '{current}'.",
                reason: ReasonCode.InvalidStateTransition,
                correlationId: correlation);
        }

        if (command is IPhaseChange change && !change.IsPhaseChangeAllowed(current))
        {
            return NuottiProblem.Conflict(
                title: "Invalid state transition",
                detail: $"Cannot change phase from {current} to {change.TargetPhase}.",
                reason: ReasonCode.InvalidStateTransition,
                correlationId: correlation);
        }

        return null;
    }

    static IReadOnlyList<SongRef> BuildCatalog(
        SetlistManifest? manifest, Guid correlation, out NuottiProblem? rejection)
    {
        rejection = null;

        if (manifest?.Songs is null || manifest.Songs.Count == 0)
        {
            rejection = NuottiProblem.UnprocessableEntity(
                title: "Invalid manifest",
                detail: "At least one song is required.",
                correlationId: correlation);
            return [];
        }

        foreach (var song in manifest.Songs)
        {
            var ctx = new ValidationContext(song);
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(song, ctx, results, validateAllProperties: true)) continue;

            var first = results[0];
            rejection = NuottiProblem.UnprocessableEntity(
                title: "Invalid manifest",
                detail: first.ErrorMessage ?? "Validation failed",
                field: first.MemberNames.FirstOrDefault(),
                correlationId: correlation);
            return [];
        }

        static string Slug(string s)
            => new((s ?? string.Empty).ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());

        return manifest.Songs
            .Select((s, i) => new SongRef(
                new SongId($"song-{i + 1}-{Slug(s.Title)}"),
                s.Title,
                s.Artist ?? string.Empty))
            .ToArray();
    }

    static GameStateChanged StateChanged(
        string session, GameStateSnapshot snapshot, CommandBase command, Guid correlation)
        => new(snapshot)
        {
            SessionCode = session,
            CausedByCommandId = command.CommandId,
            CorrelationId = correlation
        };

    /// <summary>
    /// Publishes with the runtime type. IEventBus keys subscribers by TEvent, so passing an
    /// `object` would register everything under `object` and reach no subscriber.
    /// </summary>
    Task PublishAsync(object evt, CancellationToken ct, string workspaceId) =>
        SessionMessagePublisher.PublishAsync(bus, evt, ct, workspaceId);

    void Complete(System.Diagnostics.Activity? activity, CommandBase command, GameStateSnapshot? state)
    {
        metrics?.RecordCommandApplied(command.CommandId);
        activity?.SetTag("command.applied", true);

        audit?.LogCommandApplied(command, state is null
            ? "relayed"
            : $"Phase={state.Phase}, SongIndex={state.SongIndex}, TotalAnswers={state.Tallies.Sum()}, Players={state.Scores.Count}");
    }

    static CommandResult Reject(System.Diagnostics.Activity? activity, NuottiProblem problem)
    {
        activity?.SetTag("error", true);
        activity?.SetTag("error.message", problem.Detail);
        return CommandResult.Rejected(problem);
    }
}
