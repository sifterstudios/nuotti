using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Xunit;
namespace Nuotti.Performer.Tests;

public class ScoreboardPanelTests
{
    sealed class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient(new HttpClientHandler(), disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public void Snapshot_sort_order_equals_server_order_and_ties_are_deterministic()
    {
        var factory = new DummyFactory();
        var state = new PerformerUiState(factory);

        // Unordered dictionary input (server snapshot)
        var scores = new Dictionary<string, int>
        {
            ["bob"] = 5,
            ["alice"] = 7,
            ["charlie"] = 7,
            ["zoe"] = 2
        };

        var snapshot = new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Start,
            songIndex: 1,
            currentSong: null,
            catalog: Array.Empty<SongRef>(),
            choices: Array.Empty<string>(),
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: scores,
            songStartedAtUtc: null
        );

        state.UpdateGameState(snapshot);

        var ordered = state.GetOrderedScoreboard(topN: 10).ToArray();

        // Expect points desc: 7,7,5,2 and tie between alice/charlie resolved alphabetically
        Assert.Equal(new[] { "alice", "charlie", "bob", "zoe" }, ordered.Select(x => x.id).ToArray());
        Assert.Equal(new[] { 7, 7, 5, 2 }, ordered.Select(x => x.points).ToArray());
    }

    [Fact]
    public void Delta_since_last_song_computed_from_previous_snapshot()
    {
        var factory = new DummyFactory();
        var state = new PerformerUiState(factory);

        // First song snapshot
        var s1 = new GameStateSnapshot(
            sessionCode: "dev",
            phase: Phase.Play,
            songIndex: 0,
            currentSong: null,
            catalog: Array.Empty<SongRef>(),
            choices: Array.Empty<string>(),
            hintIndex: 0,
            tallies: Array.Empty<int>(),
            scores: new Dictionary<string, int> { ["p1"] = 1, ["p2"] = 0 },
            songStartedAtUtc: null
        );
        state.UpdateGameState(s1);

        // Next song starts; songIndex increments; new cumulative scores arrive
        var s2 = s1 with
        {
            Phase = Phase.Start,
            SongIndex = 1,
            Scores = new Dictionary<string, int> { ["p1"] = 2, ["p2"] = 1 }
        };
        state.UpdateGameState(s2);

        var ordered = state.GetOrderedScoreboard(10).OrderBy(x => x.id).ToArray();
        var p1 = ordered.First(x => x.id == "p1");
        var p2 = ordered.First(x => x.id == "p2");

        Assert.Equal(2, p1.points);
        Assert.Equal(1, p1.delta); // +1 since last song
        Assert.Equal(1, p2.points);
        Assert.Equal(1, p2.delta); // +1 since last song
    }
}
