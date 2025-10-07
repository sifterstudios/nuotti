using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Forwards events to SignalR group corresponding to the session.
/// </summary>
public sealed class HubBroadcastSubscriber : IDisposable
{
    readonly IDisposable _sub;
    readonly IHubContext<QuizHub> _hub;

    public HubBroadcastSubscriber(IEventBus bus, IHubContext<QuizHub> hub)
    {
        _hub = hub;
        _sub = bus.Subscribe<AnswerSubmitted>(OnAnswerSubmittedAsync);
    }

    Task OnAnswerSubmittedAsync(AnswerSubmitted evt, CancellationToken ct)
    {
        return _hub.Clients.Group(evt.SessionCode).SendAsync("AnswerSubmitted", evt, ct);
    }

    public void Dispose() => _sub.Dispose();
}