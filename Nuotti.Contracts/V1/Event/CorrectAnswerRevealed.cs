using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Emitted by Backend when the correct answer for the current song is revealed.
/// </summary>
/// <param name="CorrectChoiceIndex">Index into current Choices array representing the correct answer.</param>
public sealed record CorrectAnswerRevealed(int CorrectChoiceIndex) : EventBase
{
    public required int CorrectChoiceIndex { get; init; } = CorrectChoiceIndex;
}
