using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.Actors;

public abstract class BaseActor : IActor
{
    readonly IHubClientFactory _hubClientFactory;
    readonly Uri _baseUri;
    readonly string _session;
    protected IHubClient? Client { get; private set; }

    protected BaseActor(IHubClientFactory hubClientFactory, Uri baseUri, string session)
    {
        _hubClientFactory = hubClientFactory;
        _baseUri = baseUri;
        _session = session;
    }

    protected abstract string Role { get; }
    protected virtual string? DisplayName => null;

    protected string SessionCode => _session;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Client = _hubClientFactory.Create(_baseUri);
        await Client.StartAsync(cancellationToken);
        await Client.JoinAsync(_session, Role, DisplayName, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Client?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
