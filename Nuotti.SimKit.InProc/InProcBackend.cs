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
        Idempotency = new InMemoryIdempotencyStore(Options.Create(new NuottiOptions()));
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
        (Bus as IDisposable)?.Dispose();
        (Idempotency as IDisposable)?.Dispose();
    }
}
