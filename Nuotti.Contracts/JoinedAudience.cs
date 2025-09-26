namespace Nuotti.Contracts;

/// <summary>
/// Sent by Audience client when joining a session.
/// </summary>
/// <param name="ConnectionId">Transport-level connection id (or audience id) assigned by Backend.</param>
/// <param name="Name">Display name chosen by the audience member.</param>
public readonly record struct JoinedAudience(string ConnectionId, string Name);
