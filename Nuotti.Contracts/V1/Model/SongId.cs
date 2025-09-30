namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Stable identifier for a song/track.
/// </summary>
/// <param name="Value">Opaque unique identifier (e.g., database id, slug, or external catalog id).</param>
public readonly record struct SongId(
    string Value
);