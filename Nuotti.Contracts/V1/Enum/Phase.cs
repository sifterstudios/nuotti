using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Enum;

/// <summary>
/// The lifecycle stage of a quiz session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Phase
{
    /// <summary>
    /// Session not started, waiting.
    /// </summary>
    Idle,
    /// <summary>
    /// Waiting room before the quiz starts
    /// </summary>
    Lobby,
    /// <summary>
    /// Start of the game or round.
    /// </summary>
    Start,
    /// <summary>
    /// Optional hint is being shown or played.
    /// </summary>
    Hint,
    /// <summary>
    /// Participants are submitting answers.
    /// </summary>
    Guessing,
    /// <summary>
    /// No longer able to guess
    /// </summary>
    Lock,
    /// <summary>
    /// The correct answer is displayed/revealed.
    /// </summary>
    Reveal,
    /// <summary>
    /// The current song is being played.
    /// </summary>
    Play,
    /// <summary>
    /// Between rounds.
    /// </summary>
    Intermission,
    /// <summary>
    ///  Session/round is complete, shows winners.
    /// </summary>
    Finished,
    
}