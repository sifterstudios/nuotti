using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class AudienceActorTimeControlTests
{
    [Fact]
    public async Task Immediate_provider_skips_delays()
    {
        var factory = new CapturingHubClientFactory();
        var options = new AudienceOptions
        {
            MinDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            DropRate = 0.0,
            RandomSeed = 321
        };
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "SESS", "Alice", options, new ImmediateTimeProvider());
        await actor.StartAsync();

        var snapshot = new GameStateSnapshot(
            sessionCode: "SESS",
            phase: Phase.Guessing,
            songIndex: 0,
            currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
            choices: new[] { "A", "B", "C", "D" },
            hintIndex: 0,
            tallies: new[] { 0, 0, 0, 0 },
            scores: null,
            songStartedAtUtc: DateTime.UtcNow
        );

        var startedAt = DateTime.UtcNow;
        await actor.OnStateAsync(snapshot);

        // no extra wait should be necessary; answer should be near-instant
        var client = factory.Client!;
        // wait briefly to allow async scheduling on thread pool
        await Task.Delay(5);
        Assert.Single(client.Answers);
        var first = client.Answers[0];
        Assert.True((first.Timestamp - startedAt).TotalMilliseconds < 20);
    }

    [Fact]
    public async Task Speed_provider_scales_delays_faster()
    {
        var factory = new CapturingHubClientFactory();
        var options = new AudienceOptions
        {
            MinDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            DropRate = 0.0,
            RandomSeed = 42
        };
        var time = new RealTimeProvider(speed: 10.0); // 10x faster => ~10ms expected
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "SESS", "Eve", options, time);
        await actor.StartAsync();

        var snapshot = new GameStateSnapshot(
            sessionCode: "SESS",
            phase: Phase.Guessing,
            songIndex: 0,
            currentSong: new SongRef(new SongId("song-1"), "Title", "Artist"),
            choices: new[] { "A", "B", "C", "D" },
            hintIndex: 0,
            tallies: new[] { 0, 0, 0, 0 },
            scores: null,
            songStartedAtUtc: DateTime.UtcNow
        );

        var startedAt = DateTime.UtcNow;
        await actor.OnStateAsync(snapshot);

        await Task.Delay(50);
        var client = factory.Client!;
        Assert.Single(client.Answers);
        var first = client.Answers[0];
        Assert.InRange((first.Timestamp - startedAt).TotalMilliseconds, 5, 40);
    }
}

file sealed class CapturingHubClientFactory : IHubClientFactory
{
    public CapturingHubClient? Client { get; private set; }
    public IHubClient Create(Uri baseAddress)
    {
        Client = new CapturingHubClient(baseAddress);
        return Client;
    }
}

file sealed class CapturingHubClient : IHubClient
{
    public Uri BaseAddress { get; }
    public List<(string Session, int ChoiceIndex, DateTime Timestamp)> Answers { get; } = new();

    public CapturingHubClient(Uri baseAddress)
    {
        BaseAddress = baseAddress;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        Answers.Add((session, choiceIndex, DateTime.UtcNow));
        return Task.CompletedTask;
    }
}
