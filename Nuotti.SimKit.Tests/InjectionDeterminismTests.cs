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
            () => LaneRandom.ForLane(seed: 1, laneIndex: 0));

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
        var runOne = LaneRandom.ForLane(seed: 7, laneIndex: 3);
        var first = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runOne)).ToList();

        var runTwo = LaneRandom.ForLane(seed: 7, laneIndex: 3);
        var second = Enumerable.Range(0, 10).Select(_ => policy.SampleDelay(runTwo)).ToList();

        second.Should().Equal(first);
        first.Distinct().Should().HaveCountGreaterThan(1, "a jittered policy must vary its samples");
    }

    [Fact]
    public void Different_lanes_get_different_sequences_from_the_same_seed()
    {
        var a = LaneRandom.ForLane(seed: 7, laneIndex: 0);
        var b = LaneRandom.ForLane(seed: 7, laneIndex: 1);

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
            // Probability 1.0 so a disconnect cycle fires on every send this test performs
            // (ApplyToSends: true; the test only exercises SubmitAnswerAsync, the send path).
            ["audience"] = new ChaosPolicy(1.0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), ApplyToSends: true)
        });

        var factory = new ChaosInjectingHubClientFactory(
            new SingleClientFactory(inner),
            chaos,
            new ImmediateTimeProvider(),
            () => LaneRandom.ForLane(seed: 2, laneIndex: 0));

        var client = factory.Create(Any);
        await client.StartAsync();
        await client.JoinAsync("dev", "audience");

        var sw = Stopwatch.StartNew();
        await client.SubmitAnswerAsync("dev", 1);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        inner.Calls.Should().Contain("stop");
    }

    [Fact]
    public void ForLane_seed_derivation_is_stable_across_process_runs()
    {
        // A prior implementation derived the per-lane seed with HashCode.Combine(seed, laneIndex).
        // .NET seeds HashCode.Combine with a random value generated once per process specifically
        // so its output is NOT reproducible across runs (the same anti-hash-flooding design as
        // randomized string.GetHashCode()) - so the same (seed, laneIndex) pair produced a
        // different Random seed every time the process started, defeating this type's entire
        // purpose. Same_seed_gives_the_same_delay_sequence above could not catch that: it only
        // compares two Random instances built in the SAME process, where the per-process entropy
        // is identical for both, so the comparison is structurally blind to this defect class.
        // Only a hardcoded expected sequence - captured once and pinned here as a literal - can
        // catch "the derived seed changes when the process restarts," because it is checked
        // against a fixed expectation that does not travel with the process.
        //
        // A second prior implementation derived the seed with `seed * 397 + laneIndex`: pure
        // integer arithmetic, so it passed this same cross-process-stability test, but adjacent
        // lanes got adjacent derived seeds, and .NET's seeded Random initializes its state
        // linearly from the seed - so consecutive lanes drew a smooth arithmetic ramp instead of
        // independent samples (see Adjacent_lanes_do_not_form_an_arithmetic_progression below,
        // which is the test that formula could not pass). Pinning literals here only catches "the
        // seed changed"; it says nothing about whether nearby seeds correlate, which is why that
        // property needs its own test rather than a bigger pinned sequence.
        var random = LaneRandom.ForLane(seed: 42, laneIndex: 0);
        var draws = Enumerable.Range(0, 5).Select(_ => random.Next()).ToList();

        draws.Should().Equal(391759428, 683020650, 1055988885, 1889306514, 1274417821);
    }

    [Fact]
    public void Adjacent_lanes_do_not_form_an_arithmetic_progression()
    {
        // The defect this guards against: a per-lane seed derived by a linear formula (e.g.
        // `seed * 397 + laneIndex`) makes each lane's first draw a fixed step away from its
        // neighbor's, because .NET's seeded Random initializes its state linearly from the seed.
        // At chaos Probability 0.05 that turns "which lanes disconnect" into an evenly-spaced
        // stripe instead of a random-looking subset. A real hash scrambles the seed enough that
        // successive first-draws have no constant difference - so if this ever regresses to a
        // linear derivation, the successive differences below collapse to few repeated values.
        //
        // The raw (unreduced) difference is not enough: NextDouble() is in [0, 1), so a ramp with
        // step s that overflows 1 wraps around, e.g. a "+0.448/lane mod 1" ramp produces raw
        // differences that alternate between exactly two values (a small negative wrap and its
        // positive complement, one apart) - two distinct values, which would incorrectly satisfy
        // a bare ">1 distinct raw difference" check. Reducing each difference mod 1 first removes
        // that wraparound artifact: an arithmetic ramp reduces to exactly one distinct value mod
        // 1, no matter how many times it wraps, while independent draws still spread across many.
        var firstDraws = Enumerable.Range(0, 12)
            .Select(lane => LaneRandom.ForLane(seed: 7, laneIndex: lane).NextDouble())
            .ToList();

        var successiveDiffsMod1 = firstDraws
            .Zip(firstDraws.Skip(1), (a, b) => b - a)
            .Select(d => Math.Round(d - Math.Floor(d), 6))
            .ToList();

        successiveDiffsMod1.Distinct().Should().HaveCountGreaterThan(2,
            "an arithmetic ramp reduces to exactly one distinct successive difference mod 1, " +
            "however many times it wraps around [0, 1); independent draws do not");
    }
}
