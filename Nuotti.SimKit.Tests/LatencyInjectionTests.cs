using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class LatencyInjectionTests
{
    [Fact]
    public async Task Send_latency_injection_forwards_every_call_to_the_inner_client_in_order()
    {
        // This used to assert that the median Stopwatch-measured elapsed time across 41 sends
        // was within 20% of the configured mean. Under ITimeProvider/ImmediateTimeProvider the
        // injected delay costs no wall-clock time at all, so that measurement no longer means
        // anything: it would just assert "well under 50ms", true for any harness bug or none.
        // What's actually worth verifying is that latency injection does not drop, reorder, or
        // duplicate calls on their way to the inner client - so we assert the recorded call
        // sequence instead.
        var innerFactory = new ImmediateHubClientFactory();
        var resolver = new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy>
        {
            ["Audience"] = new LatencyPolicy(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(30), ApplyToSends: true, ApplyToReceives: false)
        });
        var factory = new LatencyInjectingHubClientFactory(
            innerFactory,
            resolver,
            new ImmediateTimeProvider(),
            () => LaneRandom.ForLane(seed: 1, laneIndex: 0));
        var client = factory.Create(new Uri("http://localhost:5000"));

        await client.StartAsync();
        await client.JoinAsync("SESS", "Audience", name: "A-1");

        for (int i = 0; i < 41; i++)
            await client.SubmitAnswerAsync("SESS", i % 4);

        await client.StopAsync();

        var expected = new List<string> { "start", "join:Audience" };
        expected.AddRange(Enumerable.Range(0, 41).Select(i => $"answer:{i % 4}"));
        expected.Add("stop");

        Assert.Equal(expected, innerFactory.Client!.Calls);
    }

    [Fact]
    public async Task Receive_latency_injection_forwards_every_snapshot_to_the_handler_in_order()
    {
        // Same rewrite as above: the old test measured wall-clock time between firing a
        // snapshot and the wrapped handler observing it, which meant nothing once the delay
        // is a no-op under ImmediateTimeProvider. What matters now is that receive-latency
        // injection still delivers every snapshot to the handler, in the order it was fired.
        var innerFactory = new ImmediateHubClientFactory();
        var resolver = new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy>
        {
            ["Projector"] = new LatencyPolicy(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(40), ApplyToSends: false, ApplyToReceives: true)
        });
        var factory = new LatencyInjectingHubClientFactory(
            innerFactory,
            resolver,
            new ImmediateTimeProvider(),
            () => LaneRandom.ForLane(seed: 1, laneIndex: 0));
        var client = factory.Create(new Uri("http://localhost:5000"));

        await client.StartAsync();
        await client.JoinAsync("SESS", "Projector");

        var inner = innerFactory.Client!;

        var received = new List<int>();
        var tcs = new TaskCompletionSource();
        int remaining = 41;
        using var sub = client.On<GameStateSnapshot>(snapshot =>
        {
            received.Add(snapshot.SongIndex);
            if (Interlocked.Decrement(ref remaining) == 0)
                tcs.TrySetResult();
            return Task.CompletedTask;
        });

        for (int i = 0; i < 41; i++)
            await inner.FireAsync(new GameStateSnapshot("SESS", Phase.Lobby, i, null, null, 0, null, null));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Enumerable.Range(0, 41), received);

        await client.StopAsync();
    }
}

file sealed class ImmediateHubClientFactory : IHubClientFactory
{
    public ImmediateHubClient? Client { get; private set; }

    public IHubClient Create(Uri baseAddress)
    {
        Client = new ImmediateHubClient();
        return Client;
    }
}

file sealed class ImmediateHubClient : IHubClient
{
    public List<string> Calls { get; } = new();

    private Func<GameStateSnapshot, Task>? _handler;

    public Task StartAsync(CancellationToken cancellationToken = default)
    { Calls.Add("start"); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default)
    { Calls.Add("stop"); return Task.CompletedTask; }
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    { Calls.Add($"join:{role}"); return Task.CompletedTask; }
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    { Calls.Add($"answer:{choiceIndex}"); return Task.CompletedTask; }

    public IDisposable On<T>(Func<T, Task> handler)
    {
        if (typeof(T) == typeof(GameStateSnapshot))
        {
            _handler = snapshot => handler((T)(object)snapshot);
            return new D(() => _handler = null);
        }
        return new NoopDisposable();
    }

    // Awaitable, unlike the fire-and-forget void this replaces: discarding the handler's Task
    // only happened to be safe while every decorator in the chain resolved synchronously under
    // ImmediateTimeProvider. The moment anything on that path yields, an unawaited Task means the
    // mutable collections the handler writes to are touched concurrently with the caller, and any
    // handler exception becomes an unobserved faulted task instead of failing the test.
    public Task FireAsync(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot) ?? Task.CompletedTask;

    sealed class D(Action dispose) : IDisposable { public void Dispose() => dispose(); }
    sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}
