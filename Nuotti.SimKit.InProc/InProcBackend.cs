using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Eventing;
using Nuotti.Backend.Idempotency;
using Nuotti.Backend.Models;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Eventing;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// A Backend with no web host: the real SessionCommandProcessor over in-memory stores and
/// the real InMemoryEventBus.
/// </summary>
/// <remarks>
/// SessionCommandProcessor has a plain constructor, so none of Kestrel, SignalR or a port is
/// needed to exercise the true command path. InMemoryEventBus invokes subscribers
/// synchronously in registration order, which is what makes a simulated run reproducible.
/// </remarks>
public sealed class InProcBackend : IDisposable
{
    public InProcBackend()
    {
        States = new InMemoryGameStateStore();
        // InMemoryIdempotencyStore defaults to TimeProvider.System when none is supplied, which
        // is a wall-clock dependency inside an otherwise-deterministic in-proc backend (it drives
        // the idempotency TTL). Pin it to a fixed instant instead: nothing here needs the TTL to
        // actually elapse, and a wall-clock read would make two "identical" runs able to observe
        // different idempotency-window behavior depending on how long each one took to execute.
        Idempotency = new InMemoryIdempotencyStore(
            Options.Create(new NuottiOptions()),
            new FixedTimeProvider());
        Bus = new InMemoryEventBus();
        Processor = new SessionCommandProcessor(
            States,
            Idempotency,
            Bus,
            NullLogger<SessionCommandProcessor>.Instance);
    }

    public IGameStateStore States { get; }
    public IIdempotencyStore Idempotency { get; }
    public IEventBus Bus { get; }
    public ISessionCommandProcessor Processor { get; }

    public void Dispose()
    {
        // None of the four owned types implements IDisposable today (IGameStateStore,
        // IIdempotencyStore, IEventBus are interfaces with no disposal contract, and their
        // in-memory implementations hold no unmanaged or disposable resources). IDisposable
        // stays on InProcBackend itself because tests already `using` it and future
        // implementations of these stores may own something that needs releasing.
    }

    /// <summary>
    /// A TimeProvider that never advances. Keeps InProcBackend's idempotency TTL bookkeeping
    /// off the wall clock so simulated runs do not depend on how long they took to execute.
    /// </summary>
    sealed class FixedTimeProvider : TimeProvider
    {
        static readonly DateTimeOffset Epoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Epoch;
    }
}
