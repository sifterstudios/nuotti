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
        // Audience may have a display name; others pass null
        if (!string.IsNullOrWhiteSpace(name) && string.Equals(role, "audience", StringComparison.OrdinalIgnoreCase))
        {
            return _connection.InvokeAsync("CreateOrJoinWithName", session, name, cancellationToken);
        }
        return _connection.InvokeAsync("Join", session, role, name, cancellationToken);
    }

    public Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default)
        => _connection.InvokeAsync("SubmitAnswer", session, choiceIndex, cancellationToken);

    public IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler)
        => _connection.On("GameStateChanged", handler);
}