using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.SimKit.Script;

public sealed record ScriptModel
{
    public List<ScriptStep> Steps { get; init; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepKind
{
    StartSet,
    NextSong,
    GiveHint,
    LockAnswers,
    RevealAnswer,
    EndSong,
    Play,
    Stop
}

public sealed record ScriptStep
{
    public StepKind Kind { get; init; }

    // Common song identifiers
    public string? SongId { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }

    // Hint-related
    public int? HintIndex { get; init; }
    public string? HintText { get; init; }
    public string? PerformerInstructions { get; init; }

    public SongId RequireSongId()
    {
        if (string.IsNullOrWhiteSpace(SongId)) throw new InvalidOperationException("SongId is required for this step");
        return new SongId(SongId!);
    }

    public SongRef RequireSongRef()
    {
        if (string.IsNullOrWhiteSpace(SongId) || string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Artist))
            throw new InvalidOperationException("SongRef requires SongId, Title, and Artist");
        return new SongRef(new SongId(SongId!), Title!, Artist!);
    }
}