namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Starts a game.
/// Allowed phases: Lobby, Finished.
/// </summary>
public sealed record StartGame : CommandBase, IPhaseRestricted
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Lobby, Enum.Phase.Finished];
}