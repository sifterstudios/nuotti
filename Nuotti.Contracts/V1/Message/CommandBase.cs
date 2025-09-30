using Nuotti.Contracts.V1.Enum;
namespace Nuotti.Contracts.V1.Message;

/// <summary>
/// Base type for all commands. Carries idempotency and tracing fields common to every command.
/// </summary>
/// <param name="CommandId">
/// Unique idempotency identifier for the command. Re-sending the same command with the same id
/// allows consumers to de-duplicate and ensure at-most-once effects for a given intent.
/// </param>
/// <param name="SessionCode">
/// Logical session identifier (e.g., show/session-code). Used for scoping authorization, routing, and auditing.
/// </param>
/// <param name="IssuedByRole">
/// Role of the actor issuing the command (e.g., Audience, conductor, projector). Useful for auth and audit.
/// </param>
/// <param name="IssuedById">
/// Stable identifier of the actor (e.g., user id, connection id, or device id). Useful for auditing and throttling.
/// </param>
/// <param name="IssuedAtUtc">
/// UTC timestamp when the command was created. Always in UTC.
/// </param>
public abstract record CommandBase
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public required string SessionCode { get; init; }
    public required Role IssuedByRole { get; init; }
    public required string IssuedById { get; init; }
    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
}