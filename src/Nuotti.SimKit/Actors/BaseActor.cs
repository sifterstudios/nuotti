using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public abstract class BaseActor : IActor
{
    readonly IHubClientFactory _hubClientFactory;
    readonly Uri _baseUri;
    readonly string _session;
    IHubClient? _client;

    protected BaseActor(IHubClientFactory hubClientFactory, Uri baseUri, string session)
    {
        _hubClientFactory = hubClientFactory;
        _baseUri = baseUri;
        _session = session;
    }

    protected abstract string Role { get; }
    protected virtual string? DisplayName => null;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _client = _hubClientFactory.Create(_baseUri);
        await _client.StartAsync(cancellationToken);
        await _client.JoinAsync(_session, Role, DisplayName, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _client?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
