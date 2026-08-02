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
        // Seconds, not tens of milliseconds. The behaviour under test is "the configured delay was
        // skipped entirely", and with a 50ms delay that verdict is decided by thread-pool
        // scheduling on a busy machine rather than by the time provider.
        var options = new AudienceOptions
        {
            MinDelay = TimeSpan.FromSeconds(5),
            MaxDelay = TimeSpan.FromSeconds(10),
            DropRate = 0.0,
            RandomSeed = 321
        };
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "SESS", "Alice", LaneRandom.ForLane(options.RandomSeed ?? 0, 0), options, new ImmediateTimeProvider());
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

        var client = factory.Client!;
        await WaitForAnswerAsync(() => client.Answers.Count, TimeSpan.FromSeconds(2));
        Assert.Single(client.Answers);
        var first = client.Answers[0];
        // Comfortably under the 5s minimum a real provider would have waited.
        Assert.True((first.Timestamp - startedAt) < TimeSpan.FromSeconds(2),
            $"Answer took {(first.Timestamp - startedAt).TotalMilliseconds}ms; the delay was not skipped.");
    }

    [Fact]
    public async Task Speed_provider_scales_delays_faster()
    {
        var factory = new CapturingHubClientFactory();
        var options = new AudienceOptions
        {
            MinDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(2),
            DropRate = 0.0,
            RandomSeed = 42
        };
        var time = new RealTimeProvider(speed: 10.0); // 10x faster => ~200ms expected
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "SESS", "Eve", LaneRandom.ForLane(options.RandomSeed ?? 0, 0), options, time);
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

        var client = factory.Client!;
        await WaitForAnswerAsync(() => client.Answers.Count, TimeSpan.FromSeconds(2));
        Assert.Single(client.Answers);
        var first = client.Answers[0];
        // Scaled down from 2s but not skipped: the window is wide enough that a loaded machine
        // cannot push it out of range, and narrow enough that neither 2s nor 0 would pass.
        Assert.InRange((first.Timestamp - startedAt).TotalMilliseconds, 50, 1500);
    }

    /// <summary>Waits for the actor to answer rather than sleeping a guessed interval.</summary>
    static async Task WaitForAnswerAsync(Func<int> answerCount, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (answerCount() == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
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

    public IDisposable On<T>(Func<T, Task> handler)
        => new NoopDisposable();

    sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}
