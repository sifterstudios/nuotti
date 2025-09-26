namespace Nuotti.Contracts;

/// <summary>
/// Broadcast by Backend to Projector and Audience when a new multiple-choice question is pushed.
/// </summary>
/// <param name="Text">The question prompt.</param>
/// <param name="Options">Available answer options displayed in order.</param>
public readonly record struct QuestionPushed(string Text, string[] Options);
