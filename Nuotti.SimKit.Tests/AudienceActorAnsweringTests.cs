using System.Collections.Concurrent;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class AudienceActorAnsweringTests
{
    [Fact]
    public async Task Generates_one_answer_per_round_within_window()
    {
        var factory = new CapturingHubClientFactory();
        var options = new AudienceOptions
        {
            MinDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            DropRate = 0.0,
            RandomSeed = 123
        };
        var actor = new AudienceActor(factory, new Uri("http://localhost:5000"), "SESS", "Bob", options);
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

        // Wait a bit longer than max to ensure task completed
        await Task.Delay(150);

        var client = factory.Client!;
        Assert.Single(client.Answers);
        var first = client.Answers[0];
        Assert.Equal("SESS", first.Session);
        Assert.InRange((first.Timestamp - startedAt).TotalMilliseconds, 40, 200); // allow some scheduling jitter

        // Replaying the same state should not produce another answer
        await actor.OnStateAsync(snapshot);
        await Task.Delay(50);
        Assert.Single(client.Answers);
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