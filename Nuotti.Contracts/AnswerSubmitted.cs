namespace Nuotti.Contracts.V1;

/// <summary>
/// Sent by Audience to Backend when an answer is submitted.
/// </summary>
/// <param name="AudienceId">Identifier of the audience member (may mirror ConnectionId).</param>
/// <param name="ChoiceIndex">Index into the question's Options array.</param>
public readonly record struct AnswerSubmitted(string AudienceId, int ChoiceIndex);
