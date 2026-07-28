using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
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
    readonly string _session;
    readonly List<IDisposable> _busSubs = [];
    readonly object _gate = new();
    bool _started;

    public InProcHubClient(IEventBus bus, string session)
    {
        _bus = bus;
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

    // Joining is a no-op in-process: there is no group to add a connection to, and the
    // session scope is fixed at construction.
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "InProcHubClient cannot submit answers directly — a SubmitAnswer command goes " +
            "through InProcCommandEmitter, the same path the HTTP fidelity uses.");

    public IDisposable On<T>(Func<T, Task> handler)
    {
        // Fail fast on a payload type the Backend never broadcasts, matching HubWireNames.For<T>().
        _ = HubWireNames.For<T>();

        IDisposable sub = typeof(T) switch
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

        lock (_gate) _busSubs.Add(sub);
        return sub;
    }

    Task Deliver<T>(string session, T payload, Func<T, Task> handler)
    {
        bool deliver;
        lock (_gate) deliver = _started && string.Equals(session, _session, StringComparison.Ordinal);
        return deliver ? handler(payload) : Task.CompletedTask;
    }
}
