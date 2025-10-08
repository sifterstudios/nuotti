using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Script;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class BaselineScenarioTests
{
    [Fact]
    public void Baseline_yaml_parses_and_phase_flow_reaches_intermission()
    {
        // Arrange: baseline YAML content (kept in sync with Script/Baselines/baseline-single-song.yaml)
        var yaml = @"audienceCount: 20
audience:
  correctnessRatio: 0.5
  minDelay: 00:00:02
  maxDelay: 00:00:05
  dropRate: 0.0
script:
  steps:
    - kind: StartSet
    - kind: NextSong
      songId: song-001
    - kind: Play
      songId: song-001
    - kind: GiveHint
      songId: song-001
      hintIndex: 0
      hintText: 'Intro riff'
      performerInstructions: 'Hum the intro'
    - kind: LockAnswers
    - kind: RevealAnswer
      songId: song-001
      title: 'Sample Title'
      artist: 'Sample Artist'
    - kind: EndSong
      songId: song-001
";

        // Act: parse baseline scenario
        var scenario = ScriptParser.ParseBaselineYaml(yaml);

        // Assert: audience config
        Assert.Equal(20, scenario.AudienceCount);
        Assert.Equal(0.5, scenario.Audience.CorrectnessRatio, 3);
        Assert.Equal(TimeSpan.FromSeconds(2), scenario.Audience.MinDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), scenario.Audience.MaxDelay);

        // Simulate a projector receiving phase updates for a single-song happy path ending in Intermission
        var expectedPhases = new List<Phase>
        {
            Phase.Lobby,
            Phase.Start,
            Phase.Play,
            Phase.Hint,
            Phase.Guessing,
            Phase.Lock,
            Phase.Reveal,
            Phase.Intermission,
        };

        var factory = new NoopHubClientFactory();
        var projector = new ProjectorActor(factory, new Uri("http://localhost:5000"), "BASE01", expectedPhases);
        projector.StartAsync().GetAwaiter().GetResult();

        int songIndex = 0;
        // Feed snapshots with the expected phases
        foreach (var phase in expectedPhases)
        {
            var snapshot = new GameStateSnapshot(
                sessionCode: "BASE01",
                phase: phase,
                songIndex: songIndex,
                currentSong: null,
                choices: ["A", "B"],
                hintIndex: 0,
                tallies: [0, 0],
                scores: null,
                songStartedAtUtc: null);
            projector.OnStateAsync(snapshot).GetAwaiter().GetResult();
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