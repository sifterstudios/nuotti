using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Selects the CurrentSong and SongIndex for the next Round. Emitted with NextRound (and optionally Start).
/// </summary>
public sealed record CurrentSongSet(SongRef Song, int SongIndex) : EventBase
{
    public SongRef Song { get; } = Song;
    public int SongIndex { get; } = SongIndex;
}
