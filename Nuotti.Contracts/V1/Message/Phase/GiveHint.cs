using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Increments hint counter.
/// Allowed phases: Start, Hint.
/// </summary>
public sealed record GiveHint(Hint Hint) : CommandBase, IPhaseRestricted
{
    public Hint Hint { get; } = Hint;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } =
        [Enum.Phase.Start, Enum.Phase.Hint];
}