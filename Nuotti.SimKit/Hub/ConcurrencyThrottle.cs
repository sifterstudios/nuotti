using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit.Hub;

/// <summary>
/// Factory that wraps hub clients to limit the max number of concurrent send operations.
/// Currently applies to SubmitAnswerAsync only (highest volume), but can be extended.
/// </summary>
public sealed class ThrottlingHubClientFactory : IHubClientFactory
{
    private readonly IHubClientFactory _inner;
    private readonly int _maxConcurrentSends;

    /// <param name="maxConcurrentSends">Maximum number of concurrent send ops allowed. Values <= 0 mean unlimited.</param>
    public ThrottlingHubClientFactory(IHubClientFactory inner, int maxConcurrentSends)
    {
        _inner = inner;
        _maxConcurrentSends = maxConcurrentSends;
    }

    public IHubClient Create(Uri baseAddress)
        => new ThrottlingHubClient(_inner.Create(baseAddress), _maxConcurrentSends);
}

internal sealed class ThrottlingHubClient : IHubClient
{
    private readonly IHubClient _inner;
    private readonly SemaphoreSlim? _sendGate;

    public ThrottlingHubClient(IHubClient inner, int maxConcurrentSends)
    {
        _inner = inner;
        _sendGate = maxConcurrentSends > 0 ? new SemaphoreSlim(maxConcurrentSends) : null;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _inner.StopAsync(cancellationToken);

    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
        => _inner.JoinAsync(session, role, name, cancellationToken);

    public async Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
    {
        if (_sendGate is null)
        {
            await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _inner.SubmitAnswerAsync(session, choiceIndex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
        => _inner.OnGameStateChanged(handler);
}
