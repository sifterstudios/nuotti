using Nuotti.Contracts.V1.Eventing;
using System.Collections.Concurrent;
namespace Nuotti.Backend.Eventing;

/// <summary>
/// Simple in-memory event bus. Keeps ordered subscriber lists per event type and
/// invokes them synchronously in registration order. Thread-safe for subscribe/publish.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    sealed class Subscription : IDisposable
    {
        readonly InMemoryEventBus _bus;
        readonly Type _eventType;
        readonly Delegate _handler;
        bool _disposed;
        public Subscription(InMemoryEventBus bus, Type eventType, Delegate handler)
        {
            _bus = bus; _eventType = eventType; _handler = handler;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe(_eventType, _handler);
        }
    }

    readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    readonly ConcurrentDictionary<Type, object> _locks = new();

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
    {
        var type = typeof(TEvent);
        var list = _handlers.GetOrAdd(type, _ => new List<Delegate>());
        var gate = _locks.GetOrAdd(type, _ => new object());
        lock (gate)
        {
            list.Add(handler);
        }
        return new Subscription(this, type, handler);
    }

    void Unsubscribe(Type type, Delegate handler)
    {
        if (_handlers.TryGetValue(type, out var list))
        {
            var gate = _locks.GetOrAdd(type, _ => new object());
            lock (gate)
            {
                list.Remove(handler);
            }
        }
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
    {
        var type = typeof(TEvent);
        if (!_handlers.TryGetValue(type, out var list) || list.Count == 0) return;
        Delegate[] snapshot;
        var gate = _locks.GetOrAdd(type, _ => new object());
        lock (gate)
        {
            snapshot = list.ToArray();
        }
        foreach (var del in snapshot)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var func = (Func<TEvent, CancellationToken, Task>)del;
            await func(evt, cancellationToken).ConfigureAwait(false);
        }
    }
}