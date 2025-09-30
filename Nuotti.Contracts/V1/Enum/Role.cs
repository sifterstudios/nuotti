using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Enum;

/// <summary>
/// Identifies the perspective interacting with a session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    /// <summary>
    /// The band or musician running the quiz
    /// </summary>
    Performer,
    /// <summary>
    /// The display surface showing the quiz flow
    /// </summary>
    Projector,
    /// <summary>
    /// A Participant answering questions
    /// </summary>
    Audience,
    /// <summary>
    /// The audio backend component driving playback
    /// </summary>
    Engine,
}