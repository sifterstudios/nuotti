using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Backend.Workspaces;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Realtime;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Session Fan-out adapter: publishes bus messages to SignalR using <see cref="HubWireContract"/>.
/// Caller-scoped acks (Problem, AnswerAccepted, join signals) stay on QuizHub.
/// </summary>
public sealed class HubBroadcastSubscriber : IDisposable
{
    readonly List<IDisposable> _subs = [];
    readonly IHubContext<QuizHub> _hub;

    public HubBroadcastSubscriber(IEventBus bus, IHubContext<QuizHub> hub)
    {
        _hub = hub;

        _subs.Add(bus.Subscribe<GameStateChanged>((evt, ct) =>
            SendSession(evt.SessionCode, evt, ct)));

        _subs.Add(bus.Subscribe<SessionMessagePublisher.WorkspacePublication>((publication, ct) =>
            SendWorkspace(publication, ct)));

        _subs.Add(bus.Subscribe<AnswerSubmitted>((evt, ct) =>
            SendSession(evt.SessionCode, evt, ct)));

        _subs.Add(bus.Subscribe<QuestionPushed>((cmd, ct) =>
            SendSession(cmd.SessionCode, cmd, ct)));
        _subs.Add(bus.Subscribe<PlayTrack>((cmd, ct) =>
            SendSession(cmd.SessionCode, cmd, ct)));
        _subs.Add(bus.Subscribe<StopTrack>((cmd, ct) =>
            SendSession(cmd.SessionCode, cmd, ct)));
    }

    Task SendSession(string session, object message, CancellationToken ct)
    {
        if (!HubWireContract.TrySessionWire(message, out var method, out var payload))
            return Task.CompletedTask;
        return _hub.Clients.Group(RealtimeGroups.Session(session)).SendAsync(method, payload, ct);
    }

    Task SendWorkspace(SessionMessagePublisher.WorkspacePublication publication, CancellationToken ct)
    {
        if (!HubWireContract.TrySessionWire(publication.Payload, out var method, out var payload))
            return Task.CompletedTask;
        return _hub.Clients
            .Group(RealtimeGroups.Workspace(publication.WorkspaceId, publication.SessionCode))
            .SendAsync(method, payload, ct);
    }

    public void Dispose()
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();
    }
}
