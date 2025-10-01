namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Creates a session.
/// Allowed phases: Lobby.
/// <param name="SessionId">String representing the id of the session</param>
/// </summary>
public sealed record CreateSession(string SessionId) : CommandBase, IPhaseRestricted
{
    public string SessionId { get; } = SessionId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Idle];
}