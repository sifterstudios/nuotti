using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Audience submits or updates an answer.
/// Allowed phases: Guessing.
/// </summary>
public sealed record SubmitAnswer(SongId SongId) : CommandBase, IPhaseRestricted
{
    public SongId SongId { get; } = SongId;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];
}