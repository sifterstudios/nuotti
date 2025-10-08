using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class LatencyInjectionTests
{
    [Fact]
    public async Task Send_latency_median_matches_config_within_20_percent()
    {
        var innerFactory = new ImmediateHubClientFactory();
        var resolver = new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy>
        {
            ["Audience"] = new LatencyPolicy(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(30), ApplyToSends: true, ApplyToReceives: false)
        });
        var factory = new LatencyInjectingHubClientFactory(innerFactory, resolver);
        var client = factory.Create(new Uri("http://localhost:5000"));

        await client.StartAsync();
        await client.JoinAsync("SESS", "Audience", name: "A-1");

        var samples = new List<double>();
        for (int i = 0; i < 41; i++)
        {
            var sw = Stopwatch.StartNew();
            await client.SubmitAnswerAsync("SESS", i % 4);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
            await Task.Delay(5); // small gap
        }

        var median = Median(samples);
        Assert.InRange(median, 40, 60); // 50ms ±20%

        await client.StopAsync();
    }

    [Fact]
    public async Task Receive_latency_median_matches_config_within_20_percent()
    {
        var innerFactory = new ImmediateHubClientFactory();
        var resolver = new DictionaryLatencyPolicyResolver(new Dictionary<string, LatencyPolicy>
        {
            ["Projector"] = new LatencyPolicy(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(40), ApplyToSends: false, ApplyToReceives: true)
        });
        var factory = new LatencyInjectingHubClientFactory(innerFactory, resolver);
        var client = factory.Create(new Uri("http://localhost:5000"));

        await client.StartAsync();
        await client.JoinAsync("SESS", "Projector");

        var inner = innerFactory.Client!;

        var samples = new ConcurrentBag<double>();
        var tcs = new TaskCompletionSource();
        int remaining = 41;
        var starts = new ConcurrentQueue<DateTime>();
        using var sub = client.OnGameStateChanged(snapshot =>
        {
            if (!starts.TryDequeue(out var s))
                return; // shouldn't happen
            var elapsed = (DateTime.UtcNow - s).TotalMilliseconds;
            samples.Add(elapsed);
            var left = Interlocked.Decrement(ref remaining);
            if (left == 0)
                tcs.TrySetResult();
        });

        for (int i = 0; i < 41; i++)
        {
            starts.Enqueue(DateTime.UtcNow);
            inner.Fire(new GameStateSnapshot("SESS", Phase.Lobby, 0, null, null, 0, null, null));
            await Task.Delay(10);
        }

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var median = Median(samples.ToList());
        Assert.InRange(median, 64, 96); // 80ms ±20%

        await client.StopAsync();
    }

    static double Median(List<double> values)
    {
        var arr = values.ToArray();
        Array.Sort(arr);
        int n = arr.Length;
        if (n == 0) return 0;
        if (n % 2 == 1) return arr[n / 2];
        return 0.5 * (arr[n / 2 - 1] + arr[n / 2]);
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
    private Action<GameStateSnapshot>? _handler;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
    {
        _handler = handler;
        return new D(() => _handler = null);
    }

    public void Fire(GameStateSnapshot snapshot) => _handler?.Invoke(snapshot);

    sealed class D(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}
