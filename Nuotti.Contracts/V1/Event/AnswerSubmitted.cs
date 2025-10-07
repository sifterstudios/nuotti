using Nuotti.Contracts.V1.Message;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Sent by Audience to Backend when an answer is submitted.
/// </summary>
/// <param name="AudienceId">Identifier of the audience member (may mirror ConnectionId).</param>
/// <param name="ChoiceIndex">Index into the question's Options array.</param>
/// <inheritdoc cref="EventBase"/>
public sealed record AnswerSubmitted(string AudienceId, int ChoiceIndex) : EventBase
{
    public required string AudienceId { get; init; } = AudienceId;
    public required int ChoiceIndex { get; init; } = ChoiceIndex;
}