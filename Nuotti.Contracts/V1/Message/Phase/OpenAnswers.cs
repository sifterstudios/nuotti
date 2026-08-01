namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Opens a Guessing Window. Duration must be 10–120 seconds (default 30).
/// Allowed after Start/Hint, and after Lock to open another Window in the same Round.
/// </summary>
public sealed record OpenAnswers(int WindowSeconds = 30) : CommandBase, IPhaseRestricted, IPhaseChange
{
    public int WindowSeconds { get; } = WindowSeconds;

    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Start, Enum.Phase.Hint, Enum.Phase.Lock];

    public Enum.Phase TargetPhase => Enum.Phase.Guessing;
    public IReadOnlyCollection<Enum.Phase> AllowedSourcePhases =>
        [Enum.Phase.Start, Enum.Phase.Hint, Enum.Phase.Lock];
    public bool IsPhaseChangeAllowed(Enum.Phase current) => AllowedSourcePhases.Contains(current);

    public static int ClampWindowSeconds(int seconds) => Math.Clamp(seconds <= 0 ? 30 : seconds, 10, 120);
}
