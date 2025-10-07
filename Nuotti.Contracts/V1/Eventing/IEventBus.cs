namespace Nuotti.Contracts.V1.Eventing;

/// <summary>
/// In-process publish/subscribe event bus abstraction to decouple producers from side-effect handlers.
/// Guarantees that within a single Publish call, subscribers are invoked in the order they were subscribed.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Subscribe to events of the specified type. Handlers may be async.
    /// Returns an <see cref="IDisposable"/> that can be used to unsubscribe.
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler);

    /// <summary>
    /// Publish an event instance to all subscribers. Handlers are awaited in subscription order.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);
}