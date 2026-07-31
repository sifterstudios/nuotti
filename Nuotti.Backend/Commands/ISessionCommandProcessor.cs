using Nuotti.Contracts.V1.Message;
namespace Nuotti.Backend.Commands;

/// <summary>
/// Applies a Command to a Session. The only path by which a Session's state changes.
/// </summary>
/// <remarks>
/// Behind this one method sit role authorization, idempotency, the phase guard, the pure reducer,
/// persistence, audit, metrics and tracing. It never touches SignalR — every outbound message
/// leaves through <see cref="Contracts.V1.Eventing.IEventBus"/>, whose subscribers own the wire
/// contract. It never throws for an expected rejection.
/// </remarks>
public interface ISessionCommandProcessor
{
    /// <param name="session">Session code the Command applies to.</param>
    /// <param name="actor">Who is issuing it, and whether the server verified that.</param>
    /// <param name="command">The Command.</param>
    /// <param name="correlationId">
    /// Correlation id for the causal chain. Defaults to the Command's own id, which is correct for
    /// callers with no ambient correlation (e.g. a hub invocation). HTTP adapters pass the id from
    /// the request's correlation header.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<CommandResult> ApplyAsync(
        string session,
        Actor actor,
        CommandBase command,
        Guid? correlationId = null,
        CancellationToken ct = default,
        string workspaceId = "legacy");
}
