namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Starts a game.
/// Allowed phases: Lobby, Finished.
/// </summary>
public sealed record StartGame : CommandBase, IPhaseRestricted, IPhaseChange
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Lobby, Enum.Phase.Finished];

    public Enum.Phase TargetPhase => Enum.Phase.Start;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Lobby, Enum.Phase.Finished];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}