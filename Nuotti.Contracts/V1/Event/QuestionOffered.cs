using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// The question and its answer choices are now the ones on offer for the current round.
/// </summary>
/// <remarks>
/// Emitted alongside the QuestionPushed relay command. The relay carries the question to
/// clients on the wire; this event is what puts the choices into GameStateSnapshot, which
/// GameReducer needs before it can bounds-check an answer or size a tally. Before this
/// existed, Choices was never populated by any command or event, so every AnswerSubmitted
/// failed its bounds check and no tally ever moved.
/// </remarks>
/// <param name="Text">The question prompt.</param>
/// <param name="Choices">Available answer options displayed in order.</param>
public sealed record QuestionOffered(string Text, IReadOnlyList<string> Choices) : EventBase
{
    public required string Text { get; init; } = Text;
    public required IReadOnlyList<string> Choices { get; init; } = Choices;
}
