namespace Nuotti.AudioEngine.Playback.Coordinator;

public sealed class InMemoryPlaybackJournal : IPlaybackJournal
{
    readonly List<JournalEntry> _entries = [];
    public IReadOnlyList<JournalEntry> Entries => _entries;
    public void Append(JournalEntry entry) => _entries.Add(entry);
}

public sealed class InMemoryAnchorEmitter : IAnchorEmitter
{
    readonly List<PlaybackAnchorRecord> _anchors = [];
    public IReadOnlyList<PlaybackAnchorRecord> Anchors => _anchors;
    public void Emit(PlaybackAnchorRecord anchor) => _anchors.Add(anchor);
}
