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
            if (!HubWireContract.TryShowAgentCommand(publication.Payload, out var messageType))
                return;
            await store.AppendCommandAsync(publication.WorkspaceId, publication.SessionCode,
                messageType, publication.Payload, ct);
        });
    }

    public void Dispose() => _subscription.Dispose();
}
