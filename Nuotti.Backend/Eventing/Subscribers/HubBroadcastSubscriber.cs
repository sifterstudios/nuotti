using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Backend.Workspaces;
using Nuotti.Backend.Persistence;
using Nuotti.Backend.Realtime;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Owns the SignalR wire contract. Every outbound message name is declared here and nowhere else:
/// no endpoint, hub method or command effect calls IHubContext directly.
/// </summary>
public sealed class HubBroadcastSubscriber : IDisposable
{
    readonly List<IDisposable> _subs = [];
    readonly IHubContext<QuizHub> _hub;

    public HubBroadcastSubscriber(IEventBus bus, IHubContext<QuizHub> hub)
    {
        _hub = hub;

        // The snapshot push. Clients receive the bare snapshot, not the event envelope, so the
        // payload is unchanged from when endpoints sent this themselves.
        _subs.Add(bus.Subscribe<GameStateChanged>((evt, ct) =>
            Send(evt.SessionCode, "GameStateChanged", evt.Snapshot, ct)));

        _subs.Add(bus.Subscribe<SessionMessagePublisher.WorkspacePublication>((publication, ct) =>
            SendWorkspace(publication, ct)));

        // Answers carry no snapshot push: one per answer would be quadratic in audience size.
        // Clients apply GameReducer to this event to keep their tallies live.
        _subs.Add(bus.Subscribe<AnswerSubmitted>((evt, ct) =>
            Send(evt.SessionCode, "AnswerSubmitted", evt, ct)));

        // Relay commands, forwarded to the session untouched.
        _subs.Add(bus.Subscribe<QuestionPushed>((cmd, ct) =>
            Send(cmd.SessionCode, "QuestionPushed", cmd, ct)));
        _subs.Add(bus.Subscribe<PlayTrack>((cmd, ct) =>
            Send(cmd.SessionCode, "PlayTrack", cmd, ct)));
        _subs.Add(bus.Subscribe<StopTrack>((cmd, ct) =>
            Send(cmd.SessionCode, "Stop", cmd, ct)));
    }

    Task Send(string session, string method, object payload, CancellationToken ct)
        => _hub.Clients.Group(RealtimeGroups.Session(session)).SendAsync(method, payload, ct);

    Task SendWorkspace(SessionMessagePublisher.WorkspacePublication publication, CancellationToken ct)
    {
        var (method, payload) = publication.Payload switch
        {
            GameStateChanged changed => ("GameStateChanged", (object)changed.Snapshot),
            AnswerSubmitted answer => ("AnswerSubmitted", answer),
            QuestionPushed question => ("QuestionPushed", question),
            PlayTrack play => ("PlayTrack", play),
            StopTrack stop => ("Stop", stop),
            _ => (string.Empty, publication.Payload)
        };
        return method.Length == 0 ? Task.CompletedTask : _hub.Clients
            .Group(RealtimeGroups.Workspace(publication.WorkspaceId, publication.SessionCode))
            .SendAsync(method, payload, ct);
    }

    public void Dispose()
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();
    }
}
