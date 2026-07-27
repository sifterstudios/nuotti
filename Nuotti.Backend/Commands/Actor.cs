using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.Backend.Commands;

/// <summary>
/// Who is issuing a Command, and whether the server established that itself.
/// </summary>
/// <param name="Role">The role the actor is acting as.</param>
/// <param name="Id">Stable identifier of the actor (connection id, user id, or device id).</param>
/// <param name="ServerVerified">
/// True when the server determined the role itself — a hub connection whose role was recorded at
/// Join. False when the role is merely claimed by the caller, as with an HTTP request body.
/// The distinction is deliberately visible: today both are accepted, so requiring verification for
/// Performer commands later is a one-line change in one place.
/// </param>
public sealed record Actor(Role Role, string Id, bool ServerVerified)
{
    /// <summary>
    /// An actor whose role comes from the request payload and has not been verified by the server.
    /// </summary>
    public static Actor Claimed(CommandBase command)
        => new(command.IssuedByRole, command.IssuedById, ServerVerified: false);

    /// <summary>
    /// An actor whose role the server established itself, e.g. from a hub connection's Join.
    /// </summary>
    public static Actor Verified(Role role, string id)
        => new(role, id, ServerVerified: true);
}
