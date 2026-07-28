namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Opens the answer window, moving the session into Guessing.
/// Allowed phases: Start, Hint.
/// </summary>
/// <remarks>
/// Without this there was no command at all that reached Guessing, so a session could be started
/// and then never driven any further: LockAnswers, NextRound and SubmitAnswer all require Guessing,
/// and nothing produced it.
/// </remarks>
public sealed record OpenAnswers : CommandBase, IPhaseRestricted, IPhaseChange
{
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Start, Enum.Phase.Hint];

    public Enum.Phase TargetPhase => Enum.Phase.Guessing;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases => [Enum.Phase.Start, Enum.Phase.Hint];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);
}
