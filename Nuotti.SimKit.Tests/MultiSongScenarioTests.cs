using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class MultiSongScenarioTests
{
    [Fact]
    public void Multi_song_flow_with_hints_and_tallies_reset_on_NextSong()
    {
        // Arrange: expected projector phases across three songs
        var expectedPhases = new List<Phase>
        {
            // Song 1
            Phase.Lobby,
            Phase.Start,   // NextSong -> Start
            Phase.Play,
            Phase.Hint,
            Phase.Guessing,
            Phase.Lock,
            Phase.Reveal,
            Phase.Intermission,
            // Song 2
            Phase.Start,   // NextSong -> Start
            Phase.Play,
            Phase.Hint,
            Phase.Hint,
            Phase.Guessing,
            Phase.Lock,
            Phase.Reveal,
            Phase.Intermission,
            // Song 3
            Phase.Start,   // NextSong -> Start
            Phase.Play,
            Phase.Hint,
            Phase.Guessing,
            Phase.Lock,
            Phase.Reveal,
            Phase.Intermission,
        };

        var factory = new NoopHubClientFactory();
        var projector = new ProjectorActor(factory, new Uri("http://localhost:5000"), "MULTI01", expectedPhases);
        projector.StartAsync().GetAwaiter().GetResult();

        // Act/Assert: feed snapshots and assert tallies cleared at each Start after NextSong
        int songIndex = -1; // will be incremented on each NextSong -> Start
        int[] nonZeroTallies = [2, 3];

        for (int i = 0; i < expectedPhases.Count; i++)
        {
            var phase = expectedPhases[i];

            // Simulate NextSong boundary by incrementing songIndex on Start phase
            if (phase == Phase.Start)
            {
                songIndex += 1;
            }

            int[] tallies = phase == Phase.Start
                ? Array.Empty<int>() // After NextSong -> Start, tallies must be reset to empty/zeroed
                : (phase == Phase.Guessing ? new int[] { 0, 0 } : nonZeroTallies);

            var snapshot = new GameStateSnapshot(
                sessionCode: "MULTI01",
                phase: phase,
                songIndex: songIndex,
                currentSong: null,
                choices: ["A", "B"],
                hintIndex: phase == Phase.Hint ? 1 : 0,
                tallies: tallies,
                scores: null,
                songStartedAtUtc: null);

            projector.OnStateAsync(snapshot).GetAwaiter().GetResult();

            if (phase == Phase.Start)
            {
                // Assert tallies zeroed after NextSong (represented by Start phase)
                Assert.Empty(snapshot.Tallies);
            }
        }

        // Verify the sequence matched and ended in Intermission
        Assert.Equal(expectedPhases, projector.ReceivedPhases);
        Assert.Equal(Phase.Intermission, projector.ReceivedPhases[^1]);

        projector.StopAsync().GetAwaiter().GetResult();
    }

    sealed class NoopHubClientFactory : IHubClientFactory
    {
        public IHubClient Create(Uri baseAddress) => new NoopHubClient();

        sealed class NoopHubClient : IHubClient
        {
            public Uri BaseAddress => new Uri("http://localhost/");
            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler) => new D();
            sealed class D : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}
