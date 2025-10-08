using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit.Hub;

/// <summary>
/// Describes random disconnects with downtime window.
/// Each receive/send has Probability to trigger a disconnect cycle with a random downtime in [MinDowntime, MaxDowntime].
/// </summary>
public readonly record struct ChaosPolicy(double Probability, TimeSpan MinDowntime, TimeSpan MaxDowntime, bool ApplyToSends = false, bool ApplyToReceives = true)
{
    public TimeSpan SampleDowntime(Random? random = null)
    {
        random ??= Random.Shared;
        if (MaxDowntime <= TimeSpan.Zero) return TimeSpan.Zero;
        var min = MinDowntime < TimeSpan.Zero ? TimeSpan.Zero : MinDowntime;
        var max = MaxDowntime < min ? min : MaxDowntime;
        if (max == min) return max;
        var u = random.NextDouble();
        var ms = min.TotalMilliseconds + u * (max.TotalMilliseconds - min.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(ms < 0 ? 0 : ms);
    }
}

public interface IChaosPolicyResolver
{
    bool TryGetPolicy(string role, out ChaosPolicy policy);
}

public sealed class DictionaryChaosPolicyResolver : IChaosPolicyResolver
{
    private readonly IReadOnlyDictionary<string, ChaosPolicy> _policies;
    private readonly StringComparer _cmp;

    public DictionaryChaosPolicyResolver(IReadOnlyDictionary<string, ChaosPolicy> policies, StringComparer? comparer = null)
    {
        _policies = policies;
        _cmp = comparer ?? StringComparer.OrdinalIgnoreCase;
    }

    public bool TryGetPolicy(string role, out ChaosPolicy policy)
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
/// Factory that wraps hub clients with chaos disconnect injection based on role.
/// </summary>
public sealed class ChaosInjectingHubClientFactory : IHubClientFactory
{
    private readonly IHubClientFactory _inner;
    private readonly IChaosPolicyResolver _resolver;

    public ChaosInjectingHubClientFactory(IHubClientFactory inner, IChaosPolicyResolver resolver)
    {
        _inner = inner;
        _resolver = resolver;
    }

    public IHubClient Create(Uri baseAddress)
        => new ChaosInjectingHubClient(_inner.Create(baseAddress), _resolver);
}

internal sealed class ChaosInjectingHubClient : IHubClient
{
    private readonly IHubClient _inner;
    private readonly IChaosPolicyResolver _resolver;
    private ChaosPolicy? _activePolicy;
    private readonly object _gate = new();
    private string? _session; private string? _role; private string? _name;
    private bool _started;

    public ChaosInjectingHubClient(IHubClient inner, IChaosPolicyResolver resolver)
    {
        _inner = inner;
        _resolver = resolver;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _started = true;
        return _inner.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _inner.StopAsync(cancellationToken);

    public async Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        _session = session; _role = role; _name = name;
        if (_resolver.TryGetPolicy(role, out var pol))
            _activePolicy = pol;
        await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        // Maybe chaos on send
        if (_activePolicy is { } p && p.ApplyToSends && Random.Shared.NextDouble() < p.Probability)
            await DisconnectCycleAsync(p, cancellationToken).ConfigureAwait(false);
        await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
    {
        Action<GameStateSnapshot> wrapped = async snapshot =>
        {
            var p = _activePolicy;
            if (p is { } pp && pp.ApplyToReceives && Random.Shared.NextDouble() < pp.Probability)
            {
                await DisconnectCycleAsync(pp).ConfigureAwait(false);
            }
            handler(snapshot);
        };
        return _inner.OnGameStateChanged(wrapped);
    }

    private async Task DisconnectCycleAsync(ChaosPolicy policy, CancellationToken cancellationToken = default)
    {
        // Ensure only one cycle at a time
        bool doCycle = false;
        lock (_gate)
        {
            // If not started or not joined, skip
            if (_started && _session is not null)
                doCycle = true;
        }
        if (!doCycle) return;

        try
        {
            await _inner.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch { /* ignore */ }

        var down = policy.SampleDowntime();
        if (down > TimeSpan.Zero)
            await Task.Delay(down, cancellationToken).ConfigureAwait(false);

        try
        {
            await _inner.StartAsync(cancellationToken).ConfigureAwait(false);
            // Re-join with stored parameters
            var session = _session!; var role = _role!; var name = _name;
            await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // If restart failed, try one more time shortly to avoid being stuck
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            await _inner.StartAsync(cancellationToken).ConfigureAwait(false);
            var session = _session!; var role = _role!; var name = _name;
            await _inner.JoinAsync(session, role, name, cancellationToken).ConfigureAwait(false);
        }
    }
}
