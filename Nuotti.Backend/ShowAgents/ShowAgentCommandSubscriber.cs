using Nuotti.Backend.Persistence;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Backend.ShowAgents;

public sealed class ShowAgentCommandSubscriber : IDisposable
{
    readonly IDisposable _subscription;

    public ShowAgentCommandSubscriber(IEventBus bus, IShowAgentAccessStore store)
    {
        _subscription = bus.Subscribe<SessionMessagePublisher.WorkspacePublication>(async (publication, ct) =>
        {
            var messageType = publication.Payload switch
            {
                PlayTrack => "PlayTrack",
                StopTrack => "StopTrack",
                PreparePlayback => "Prepare",
                _ => null
            };
            if (messageType is not null)
                await store.AppendCommandAsync(publication.WorkspaceId, publication.SessionCode,
                    messageType, publication.Payload, ct);
        });
    }

    public void Dispose() => _subscription.Dispose();
}
