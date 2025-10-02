namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Base type for all events. Encapsulates event identity and tracing details.
/// </summary>
/// <param name="EventId">
/// Unique identifier of the event for idempotency in publication and downstream processing.
/// </param>
/// <param name="CorrelationId">
/// Correlation id across a causal chain. For an initial command, set to its <see cref="CommandBase.CommandId"/>.
/// All subsequent messages caused by that command should carry the same value.
/// </param>
/// <param name="CausedByCommandId">
/// The immediate command that caused this event. Enables precise causal tracing and debugging.
/// </param>
/// <param name="SessionCode">
/// Logical session identifier, mirrored from the causing command for ease of consumption.
/// </param>
/// <param name="EmittedAtUtc">
/// UTC timestamp when the event was emitted/persisted. Always in UTC.
/// </param>
public abstract record EventBase
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required Guid CorrelationId { get; init; }
    public required Guid CausedByCommandId { get; init; }
    public required string SessionCode { get; init; }
    public DateTime EmittedAtUtc { get; init; } = DateTime.UtcNow;
};