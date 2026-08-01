using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// An <see cref="IHubClient"/> backed by the in-process <see cref="IEventBus"/> instead of a
/// SignalR connection.
/// </summary>
/// <remarks>
/// Mirrors HubBroadcastSubscriber's translation exactly — same payload for each broadcast, and
/// the same per-session scoping the real hub gets from Clients.Group(session). Getting either
/// wrong would make in-memory runs disagree with real ones for reasons unrelated to the code
/// under test.
///
/// There is no connection to open, so StartAsync and StopAsync only gate delivery: a client
/// that has not started, or has stopped, receives nothing. That is what makes the chaos
/// decorator's disconnect cycle observable in-process.
/// </remarks>
public sealed class InProcHubClient : IHubClient
{
    readonly IEventBus _bus;
    readonly ISessionCommandProcessor _processor;
    readonly string _session;
    readonly object _gate = new();
    bool _started;
    string? _role;
    string? _participantId;

    public InProcHubClient(IEventBus bus, ISessionCommandProcessor processor, string session)
    {
        _bus = bus;
        _processor = processor;
        _session = session;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) _started = false;
        return Task.CompletedTask;
    }

    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(session, _session, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"This client is bound to session '{_session}' and cannot join '{session}'. " +
                "The real hub would add the connection to a different group; failing loudly " +
                "here keeps the two fidelities honest.");

        _role = role;
        // Deterministic stand-in for SignalR's ConnectionId: the display name when there is
        // one, the role otherwise. No GUIDs — a run must be reproducible.
        _participantId = name ?? role;
        return Task.CompletedTask;
    }

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        // Mirrors QuizHub.SubmitAnswer: only an audience may answer, and the actor is
        // Verified rather than Claimed because the role was established at Join.
        if (!string.Equals(_role, "audience", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Only an audience may submit an answer; this client joined as '{_role ?? "(not joined)"}'.");

        var command = new SubmitAnswer(SongId: null, ChoiceIndex: choiceIndex)
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = _participantId!
        };

        var result = await _processor
            .ApplyAsync(session, Actor.Verified(Role.Audience, _participantId!), command, correlationId: null, cancellationToken)
            .ConfigureAwait(false);

        if (result.Outcome == Outcome.Rejected)
            throw new CommandRejectedException(command, result.Problem?.Detail ?? "rejected")
            {
                Problem = result.Problem
            };
    }

    public IDisposable On<T>(Func<T, Task> handler)
    {
        // Fail fast on a payload type the Backend never broadcasts, matching HubWireNames.For<T>().
        _ = HubWireNames.For<T>();

        return typeof(T) switch
        {
            var t when t == typeof(GameStateSnapshot) =>
                _bus.Subscribe<GameStateChanged>((evt, ct) =>
                    Deliver(evt.SessionCode, (T)(object)evt.Snapshot, handler)),
            var t when t == typeof(AnswerSubmitted) =>
                _bus.Subscribe<AnswerSubmitted>((evt, ct) => Deliver(evt.SessionCode, (T)(object)evt, handler)),
            var t when t == typeof(QuestionPushed) =>
                _bus.Subscribe<QuestionPushed>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            var t when t == typeof(PlayTrack) =>
                _bus.Subscribe<PlayTrack>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            var t when t == typeof(StopTrack) =>
                _bus.Subscribe<StopTrack>((cmd, ct) => Deliver(cmd.SessionCode, (T)(object)cmd, handler)),
            _ => throw new NotSupportedException($"No in-proc subscription for {typeof(T).Name}."),
        };
    }

    Task Deliver<T>(string session, T payload, Func<T, Task> handler)
    {
        bool deliver;
        lock (_gate) deliver = _started && string.Equals(session, _session, StringComparison.Ordinal);
        return deliver ? handler(payload) : Task.CompletedTask;
    }
}
