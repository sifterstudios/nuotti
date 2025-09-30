using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Enum;

/// <summary>
/// The lifecycle stage of a quiz session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Phase
{
    /// <summary>
    /// Waiting room before the quiz starts
    /// </summary>
    Lobby,
    /// <summary>
    /// All clients are ready; about to start
    /// </summary>
    Ready,
    /// <summary>
    /// Short lead-in before the question appears or audio begins.
    /// </summary>
    SongIntro,
    /// <summary>
    /// Optional hint is being shown or played.
    /// </summary>
    Hint,
    /// <summary>
    /// Participants are submitting answers.
    /// </summary>
    Guessing,
    /// <summary>
    /// The correct answer is displayed/revealed.
    /// </summary>
    Reveal,
    /// <summary>
    /// Between questions/rounds.
    /// </summary>
    Intermission,
    /// <summary>
    ///  Session/round is complete.
    /// </summary>
    Finished,
    
}