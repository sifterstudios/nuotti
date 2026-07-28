using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Time;
namespace Nuotti.SimKit.Hub;

/// <summary>
/// Describes artificial latency with jitter.
/// Mean +/- Jitter will be sampled uniformly per operation.
/// </summary>
public readonly record struct LatencyPolicy(TimeSpan Mean, TimeSpan Jitter, bool ApplyToSends = true, bool ApplyToReceives = true)
{
    public TimeSpan SampleDelay(Random random)
    {
        if (Jitter <= TimeSpan.Zero)
            return Mean < TimeSpan.Zero ? TimeSpan.Zero : Mean;
        var min = Mean - Jitter;
        if (min < TimeSpan.Zero) min = TimeSpan.Zero;
        var max = Mean + Jitter;
        var rangeMs = max.TotalMilliseconds - min.TotalMilliseconds;
        var u = random.NextDouble();
        var ms = min.TotalMilliseconds + u * rangeMs;
        if (ms < 0) ms = 0;
        // Round up to avoid systematic negative bias due to timer rounding
        var msCeil = Math.Ceiling(ms);
        return TimeSpan.FromMilliseconds(msCeil);
    }
}

/// <summary>
/// Resolves latency policy for a given role (e.g., "Audience", "Performer", "Projector").
/// </summary>
public interface ILatencyPolicyResolver
{
    bool TryGetPolicy(string role, out LatencyPolicy policy);
}

/// <summary>
/// Simple dictionary-based resolver.
/// </summary>
public sealed class DictionaryLatencyPolicyResolver : ILatencyPolicyResolver
{
    private readonly IReadOnlyDictionary<string, LatencyPolicy> _policies;
    private readonly StringComparer _cmp;

    public DictionaryLatencyPolicyResolver(IReadOnlyDictionary<string, LatencyPolicy> policies, StringComparer? comparer = null)
    {
        _policies = policies;
        _cmp = comparer ?? StringComparer.OrdinalIgnoreCase;
    }

    public bool TryGetPolicy(string role, out LatencyPolicy policy)
    {
        foreach (var kv in _policies)
        {
            if (_cmp.Equals(kv.Key, role))
            {
                policy = kv.Value;
                return true;
            }
        }
        policy = default;
        return false;
    }
}

/// <summary>
/// Factory that wraps produced hub clients with latency injection based on the role used when joining.
/// </summary>
public sealed class LatencyInjectingHubClientFactory : IHubClientFactory
{
    private readonly IHubClientFactory _inner;
    private readonly ILatencyPolicyResolver _resolver;
    private readonly ITimeProvider _time;
    private readonly Func<Random> _randomForClient;

    public LatencyInjectingHubClientFactory(
        IHubClientFactory inner,
        ILatencyPolicyResolver resolver,
        ITimeProvider time,
        Func<Random> randomForClient)
    {
        _inner = inner;
        _resolver = resolver;
        _time = time;
        _randomForClient = randomForClient;
    }

    public IHubClient Create(Uri baseAddress)
        => new LatencyInjectingHubClient(_inner.Create(baseAddress), _resolver, _time, _randomForClient());
}

internal sealed class LatencyInjectingHubClient : IHubClient
{
    private readonly IHubClient _inner;
    private readonly ILatencyPolicyResolver _resolver;
    private readonly ITimeProvider _time;
    private readonly Random _random;
    private LatencyPolicy? _activePolicy;

    public LatencyInjectingHubClient(
        IHubClient inner, ILatencyPolicyResolver resolver, ITimeProvider time, Random random)
    {
        _inner = inner;
        _resolver = resolver;
        _time = time;
        _random = random;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _inner.StopAsync(cancellationToken);

    public async Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        if (_resolver.TryGetPolicy(role, out var policy))
            _activePolicy = policy;
        if (_activePolicy is { ApplyToSends: true } p)
            await _time.Delay(p.SampleDelay(_random), cancellationToken).ConfigureAwait(false);
        await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        if (_activePolicy is { ApplyToSends: true } p)
            await _time.Delay(p.SampleDelay(_random), cancellationToken).ConfigureAwait(false);
        await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable On<T>(Func<T, Task> handler)
    {
        return _inner.On<T>(async payload =>
        {
            if (_activePolicy is { ApplyToReceives: true } p)
                await _time.Delay(p.SampleDelay(_random)).ConfigureAwait(false);
            await handler(payload).ConfigureAwait(false);
        });
    }
}
