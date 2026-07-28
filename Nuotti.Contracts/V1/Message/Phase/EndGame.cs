namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Ends the game, moving the session into Finished so the winners can be shown.
/// Allowed phases: Intermission.
/// </summary>
/// <remarks>
/// Finished was previously unreachable: StartGame accepts it as a source phase, so the design
/// clearly intended a game to end and then be restarted, but no command ever produced it.
/// </remarks>
public sealed record EndGame : CommandBase, IPhaseRestricted, IPhaseChange
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Intermission];

    public Enum.Phase TargetPhase => Enum.Phase.Finished;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Intermission];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}
