using System.Diagnostics;
using FluentAssertions;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class InjectionDeterminismTests
{
    sealed class RecordingHubClient : IHubClient
    {
        public List<string> Calls { get; } = new();
        public Task StartAsync(CancellationToken cancellationToken = default)
        { Calls.Add("start"); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default)
        { Calls.Add("stop"); return Task.CompletedTask; }
        public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
        { Calls.Add($"join:{role}"); return Task.CompletedTask; }
        public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        { Calls.Add($"answer:{choiceIndex}"); return Task.CompletedTask; }
        public IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler) => new Noop();
        sealed class Noop : IDisposable { public void Dispose() { } }
    }

    sealed class SingleClientFactory(IHubClient client) : IHubClientFactory
    {
        public IHubClient Create(Uri baseAddress) => client;
    }

    static readonly Uri Any = new("http://localhost:5240");

    static LatencyPolicy SlowPolicy => new(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(400));

    static ILatencyPolicyResolver ResolverFor(LatencyPolicy policy) =>
        new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy> { ["audience"] = policy });

    [Fact]
    public async Task Immediate_time_provider_means_latency_costs_no_wall_clock()
    {
        var inner = new RecordingHubClient();
        var factory = new LatencyInjectingHubClientFactory(
            new SingleClientFactory(inner),
            ResolverFor(SlowPolicy),
            new ImmediateTimeProvider(),
            () => DeterministicRandom.ForLane(seed: 1, laneIndex: 0));

        var client = factory.Create(Any);
        var sw = Stopwatch.StartNew();
        await client.JoinAsync("dev", "audience");
        for (var i = 0; i < 20; i++) await client.SubmitAnswerAsync("dev", i);
        sw.Stop();

        // 21 operations at a 500ms mean would be over ten seconds of real sleeping.
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        inner.Calls.Should().HaveCount(21);
    }

    [Fact]
    public void Same_seed_gives_the_same_delay_sequence()
    {
        var policy = SlowPolicy;

        // One Random per run, drawn from ten times. Creating a fresh Random inside the loop
        // would yield ten identical values and the test would pass without proving anything.
        var runOne = DeterministicRandom.ForLane(seed: 7, laneIndex: 3);
        var first = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runOne)).ToList();

        var runTwo = DeterministicRandom.ForLane(seed: 7, laneIndex: 3);
        var second = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runTwo)).ToList();

        second.Should().Equal(first);
        first.Distinct().Should().HaveCountGreaterThan(1, "a jittered policy must vary its samples");
    }

    [Fact]
    public void Different_lanes_get_different_sequences_from_the_same_seed()
    {
        var a = DeterministicRandom.ForLane(seed: 7, laneIndex: 0);
        var b = DeterministicRandom.ForLane(seed: 7, laneIndex: 1);

        var fromA = Enumerable.Range(0, 5).Select(_ => a.Next()).ToList();
        var fromB = Enumerable.Range(0, 5).Select(_ => b.Next()).ToList();

        fromB.Should().NotEqual(fromA);
    }

    [Fact]
    public async Task Chaos_downtime_also_costs_no_wall_clock_under_immediate_time()
    {
        var inner = new RecordingHubClient();
        var chaos = new DictionaryChaosPolicyResolver(new Dictionary<string, ChaosPolicy>
        {
            // Probability 1.0 so a disconnect cycle fires on every receive-eligible operation.
            ["audience"] = new ChaosPolicy(1.0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), ApplyToSends: true)
        });

        var factory = new ChaosInjectingHubClientFactory(
            new SingleClientFactory(inner),
            chaos,
            new ImmediateTimeProvider(),
            () => DeterministicRandom.ForLane(seed: 2, laneIndex: 0));

        var client = factory.Create(Any);
        await client.StartAsync();
        await client.JoinAsync("dev", "audience");

        var sw = Stopwatch.StartNew();
        await client.SubmitAnswerAsync("dev", 1);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        inner.Calls.Should().Contain("stop");
    }
}
