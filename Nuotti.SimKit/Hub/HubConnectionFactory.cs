using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit.Hub;

public sealed class HubConnectionFactory : IHubClientFactory
{
    readonly string _hubPath;

    public HubConnectionFactory(string hubPath = "/hub")
    {
        _hubPath = hubPath;
    }

    public IHubClient Create(Uri baseAddress)
    {
        if (!baseAddress.IsAbsoluteUri)
            throw new ArgumentException("Base address must be absolute URI", nameof(baseAddress));

        var hubUrl = new Uri(baseAddress, _hubPath);
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
        return new RealHubClient(connection);
    }
}

internal sealed class RealHubClient : IHubClient
{
    readonly HubConnection _connection;

    public RealHubClient(HubConnection connection)
    {
        _connection = connection;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _connection.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _connection.StopAsync(cancellationToken);

    public Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default)
    {
        // Audience may have a display name; others pass null. Device secret is stable per client
        // instance so SimKit reconnects restore the same Participant within the Session.
        if (string.Equals(role, "audience", StringComparison.OrdinalIgnoreCase))
        {
            var deviceSecret = _deviceSecret ??= Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(name))
                return _connection.InvokeAsync("CreateOrJoinWithName", session, name, deviceSecret, cancellationToken);
            return _connection.InvokeAsync("Join", session, role, name, deviceSecret, cancellationToken);
        }
        return _connection.InvokeAsync("Join", session, role, name, null, cancellationToken);
    }

    string? _deviceSecret;

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => _connection.InvokeAsync("SubmitAnswer", session, choiceIndex, Guid.Empty, cancellationToken);

    public IDisposable On<T>(Func<T, Task> handler)
        => _connection.On(HubWireNames.For<T>(), handler);
}
