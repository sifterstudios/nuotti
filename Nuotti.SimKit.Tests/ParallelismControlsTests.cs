using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;
using Nuotti.SimKit.Time;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class ParallelismControlsTests
{
    [Fact]
    public async Task ThrottlingHubClient_caps_concurrent_SubmitAnswerAsync()
    {
        var inner = new CountingBlockingHubClient(maxConcurrent: out var maxObserved, out var signal);
        IHubClientFactory innerFactory = new SingleClientFactory(inner);
        var throttledFactory = new ThrottlingHubClientFactory(innerFactory, maxConcurrentSends: 4);
        var client = throttledFactory.Create(new Uri("http://localhost:5000"));

        await client.StartAsync();
        await client.JoinAsync("S", "Audience", name: "A");

        // Fire many sends; they will block inside inner client until we release them.
        var tasks = new List<Task>();
        for (int i = 0; i < 25; i++)
            tasks.Add(client.SubmitAnswerAsync("S", i % 4));

        // Give a brief moment for tasks to queue and enter.
        await Task.Delay(50);

        // At this point, throttling should cap overlap at 4.
        Assert.True(maxObserved.Value <= 4, $"Observed overlap {maxObserved.Value} exceeds cap");

        // Release all blocked operations and await completion.
        signal.ReleaseAll();
        await Task.WhenAll(tasks);

        await client.StopAsync();
    }

    [Fact]
    public async Task AudienceWaveOrchestrator_batches_in_configured_waves()
    {
        int audienceCount = 23;
        int waveSize = 5;
        var waveInterval = TimeSpan.FromMilliseconds(10);
        var time = new CountingTimeProvider();

        // Prepare minimal no-op hub factory
        var hubFactory = new NoopHubClientFactory();
        var baseUri = new Uri("http://localhost:5000");
        var session = "WAVE";

        // Create audiences with zero internal delay
        var audiences = new List<AudienceActor>();
        for (int i = 0; i < audienceCount; i++)
        {
            var opts = new AudienceOptions { MinDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero, DropRate = 0 };
            var actor = new AudienceActor(hubFactory, baseUri, session, $"A-{i}", opts, new ImmediateTimeProvider());
            await actor.StartAsync();
            audiences.Add(actor);
        }

        var orchestrator = new AudienceWaveOrchestrator(audiences, waveSize, waveInterval, time);

        var snapshot = new GameStateSnapshot(session, Phase.Guessing, 1, null, new[] { "A", "B" }, 0, new[] { 0, 0 }, null, null);
        await orchestrator.DispatchAsync(snapshot);

        // Expected number of intervals is waves-1
        int expectedWaves = (int)Math.Ceiling(audienceCount / (double)waveSize);
        int expectedIntervals = Math.Max(0, expectedWaves - 1);
        Assert.Equal(expectedIntervals, time.DelayCallCount);

        // And our noop hub saw the expected number of answers submitted
        Assert.Equal(audienceCount, hubFactory.TotalSubmits);

        // Cleanup
        foreach (var a in audiences)
            await a.StopAsync();
    }

    // Helpers

    private sealed class SingleClientFactory : IHubClientFactory
    {
        private readonly IHubClient _client;
        public SingleClientFactory(IHubClient c) { _client = c; }
        public IHubClient Create(Uri baseAddress) => _client;
    }

    private sealed class CountingBlockingHubClient : IHubClient
    {
        private readonly BlockingSignal _signal;
        private int _current;
        private int _max;
        public CountingBlockingHubClient(out AtomicInt maxConcurrent, out BlockingSignal signal)
        {
            _signal = new BlockingSignal();
            signal = _signal;
            maxConcurrent = new AtomicInt(() => _max);
        }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _current);
            int snap;
            do
            {
                snap = _max;
                if (now <= snap) break;
            } while (Interlocked.CompareExchange(ref _max, now, snap) != snap);

            await _signal.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _current);
        }
        public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler) => new D();
        private sealed class D : IDisposable { public void Dispose() { } }
    }

    private sealed class BlockingSignal
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task WaitAsync(CancellationToken ct) => _tcs.Task.WaitAsync(ct);
        public void ReleaseAll() => _tcs.TrySetResult();
    }

    private sealed class AtomicInt
    {
        private readonly Func<int> _get;
        public AtomicInt(Func<int> get) { _get = get; }
        public int Value => _get();
    }

    private sealed class NoopHubClientFactory : IHubClientFactory
    {
        private int _totalSubmits;
        public int TotalSubmits => _totalSubmits;
        public IHubClient Create(Uri baseAddress)
        {
            return new NoopHubClient(this);
        }

        public sealed class NoopHubClient : IHubClient
        {
            private readonly NoopHubClientFactory _owner;
            public NoopHubClient(NoopHubClientFactory owner) { _owner = owner; }
            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _owner._totalSubmits);
                return Task.CompletedTask;
            }
            public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler) => new D();
            private sealed class D : IDisposable { public void Dispose() { } }
        }
    }

    private sealed class CountingTimeProvider : ITimeProvider
    {
        private int _delayCallCount;
        public int DelayCallCount => _delayCallCount;
        public DateTime UtcNow => DateTime.UtcNow;
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _delayCallCount);
            return Task.CompletedTask; // no actual wait
        }
    }
}
