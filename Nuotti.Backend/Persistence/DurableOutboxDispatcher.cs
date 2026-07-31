using Nuotti.Contracts.V1.Eventing;

namespace Nuotti.Backend.Persistence;

public sealed class DurableOutboxDispatcher(
    IDurableSessionCommitStore store,
    IEventBus bus,
    ILogger<DurableOutboxDispatcher> logger)
{
    readonly Guid _owner = Guid.NewGuid();

    public async Task<int> DispatchPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var delivered = 0;
        foreach (var message in await store.ClaimPendingAsync(_owner, TimeSpan.FromSeconds(30), limit, cancellationToken))
        {
            try
            {
                var payload = SessionMessagePublisher.DeserializeDurable(message.MessageType, message.Payload);
                await SessionMessagePublisher.PublishAsync(
                    bus, payload, cancellationToken, message.WorkspaceId);
                await store.MarkDeliveredAsync(message, _owner, cancellationToken);
                delivered++;
            }
            catch (System.Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox delivery failed for {Session}/{Sequence}",
                    message.SessionCode, message.Sequence.Value);
                // This lease remains until expiry so the same Session cannot overtake it. Other
                // Sessions in this claimed batch are independent and should continue immediately.
                continue;
            }
        }
        return delivered;
    }
}

public sealed class DurableOutboxWorker(
    DurableOutboxDispatcher dispatcher,
    ILogger<DurableOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delivered = await dispatcher.DispatchPendingAsync(cancellationToken: stoppingToken);
                if (delivered == 0) await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Durable outbox dispatch loop failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
