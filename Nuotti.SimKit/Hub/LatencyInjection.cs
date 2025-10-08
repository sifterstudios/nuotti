using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit.Hub;

/// <summary>
/// Describes artificial latency with jitter.
/// Mean +/- Jitter will be sampled uniformly per operation.
/// </summary>
public readonly record struct LatencyPolicy(TimeSpan Mean, TimeSpan Jitter, bool ApplyToSends = true, bool ApplyToReceives = true)
{
    public TimeSpan SampleDelay(Random? random = null)
    {
        random ??= Random.Shared;
        if (Jitter <= TimeSpan.Zero)
            return Mean < TimeSpan.Zero ? TimeSpan.Zero : Mean;
        var min = Mean - Jitter;
        if (min < TimeSpan.Zero) min = TimeSpan.Zero;
        var max = Mean + Jitter;
        var rangeMs = max.TotalMilliseconds - min.TotalMilliseconds;
        var u = random.NextDouble();
        var ms = min.TotalMilliseconds + u * rangeMs;
        if (ms < 0) ms = 0;
        return TimeSpan.FromMilliseconds(ms);
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

    public LatencyInjectingHubClientFactory(IHubClientFactory inner, ILatencyPolicyResolver resolver)
    {
        _inner = inner;
        _resolver = resolver;
    }

    public IHubClient Create(Uri baseAddress)
        => new LatencyInjectingHubClient(_inner.Create(baseAddress), _resolver);
}

internal sealed class LatencyInjectingHubClient : IHubClient
{
    private readonly IHubClient _inner;
    private readonly ILatencyPolicyResolver _resolver;
    private LatencyPolicy? _activePolicy;

    public LatencyInjectingHubClient(IHubClient inner, ILatencyPolicyResolver resolver)
    {
        _inner = inner;
        _resolver = resolver;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _inner.StopAsync(cancellationToken);

    public async Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        if (_resolver.TryGetPolicy(role, out var policy))
            _activePolicy = policy;
        // Join is a send; apply if configured
        if (_activePolicy is { } p && p.ApplyToSends)
            await Task.Delay(p.SampleDelay(), cancellationToken).ConfigureAwait(false);
        await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        if (_activePolicy is { } p && p.ApplyToSends)
            await Task.Delay(p.SampleDelay(), cancellationToken).ConfigureAwait(false);
        await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
    {
        // If receive delays are enabled, wrap the handler.
        if (_activePolicy is { } pWhenSub && pWhenSub.ApplyToReceives)
        {
            return _inner.OnGameStateChanged(async snapshot =>
            {
                var p = _activePolicy; // capture latest after Join
                if (p is { } pp && pp.ApplyToReceives)
                {
                    // Run delay then invoke handler on thread pool to avoid deadlocks with SignalR context.
                    var delay = pp.SampleDelay();
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                handler(snapshot);
            });
        }
        else
        {
            return _inner.OnGameStateChanged(handler);
        }
    }
}
