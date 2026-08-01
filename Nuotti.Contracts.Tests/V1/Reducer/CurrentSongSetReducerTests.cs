using FluentAssertions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Reducer;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Reducer;

public class CurrentSongSetReducerTests
{
    [Fact]
    public void CurrentSongSet_updates_SongIndex_CurrentSong_and_resets_HintIndex()
    {
        var song = new SongRef(new SongId("s2"), "Two", "Band");
        var state = GameReducer.Initial("S") with
        {
            SongIndex = 0,
            HintIndex = 2,
            CurrentSong = new SongRef(new SongId("s1"), "One", "Band")
        };

        var (next, error) = GameReducer.Reduce(state, new CurrentSongSet(song, 1)
        {
            SessionCode = "S",
            CausedByCommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid()
        });

        error.Should().BeNull();
        next.SongIndex.Should().Be(1);
        next.CurrentSong.Should().Be(song);
        next.HintIndex.Should().Be(0);
    }
}
