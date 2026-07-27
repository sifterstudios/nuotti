using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Emitted by Backend after a Command has been applied and the session snapshot has changed.
/// Carries the resulting snapshot so subscribers need not read the store.
/// </summary>
/// <remarks>
/// This is the event behind the "GameStateChanged" hub message. Broadcast subscribers send
/// <see cref="Snapshot"/> itself on the wire, not this envelope, so the payload clients receive
/// is unchanged.
/// </remarks>
/// <param name="Snapshot">The session state after the Command was applied.</param>
public sealed record GameStateChanged(GameStateSnapshot Snapshot) : EventBase;
