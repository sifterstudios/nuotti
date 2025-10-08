using System.Text.Json.Serialization;
namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Declarative scenario for simulations: sessions, songs, phases and audience profile.
/// </summary>
public sealed record ScenarioModel
{
    public List<SessionModel> Sessions { get; init; } = new();
    public List<SongModel> Songs { get; init; } = new();
    public AudienceProfile? Audience { get; init; }
}

/// <summary>
/// A simulation session definition with a playlist referencing songs by id.
/// </summary>
public sealed record SessionModel
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public List<SessionSongRef> Playlist { get; init; } = new();
}

/// <summary>
/// Lightweight reference to a song within a session playlist.
/// Uses string SongId to keep JSON shape simple ("songId": "id").
/// </summary>
public sealed record SessionSongRef
{
    public string SongId { get; init; } = string.Empty;
}

/// <summary>
/// Song definition used by the scenario.
/// </summary>
public sealed record SongModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public List<PhaseModel> Phases { get; init; } = new();
}

/// <summary>
/// Phase inside a song with a duration in milliseconds.
/// </summary>
public sealed record PhaseModel
{
    public string Name { get; init; } = string.Empty;
    public int DurationMs { get; init; }
}

/// <summary>
/// Audience profile data for tuning simulations.
/// </summary>
public sealed record AudienceProfile
{
    public string? Demographic { get; init; }
    public int? ExpectedSize { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnergyLevel? Energy { get; init; }
}

/// <summary>
/// Coarse energy level for an audience.
/// </summary>
public enum EnergyLevel
{
    Low,
    Medium,
    High
}
