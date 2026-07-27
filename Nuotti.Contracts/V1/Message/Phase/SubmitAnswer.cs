using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Audience submits or updates an answer.
/// Allowed phases: Guessing.
/// </summary>
/// <param name="SongId">
/// Optional context for which song is being answered. Not required: an answer always applies to
/// the session's current song, and the resulting AnswerSubmitted event does not carry it.
/// Hub callers pass null.
/// </param>
/// <param name="ChoiceIndex">Index into the current question's choices.</param>
public sealed record SubmitAnswer(SongId? SongId, int ChoiceIndex) : CommandBase, IPhaseRestricted
{
    public SongId? SongId { get; } = SongId;
    public int ChoiceIndex { get; } = ChoiceIndex;
    public IReadOnlyCollection<Enum.Phase> AllowedPhases { get; } = [Enum.Phase.Guessing];
}
