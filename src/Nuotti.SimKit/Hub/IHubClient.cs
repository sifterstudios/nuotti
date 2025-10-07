using Microsoft.AspNetCore.SignalR.Client;

namespace Nuotti.SimKit.Hub;

public interface IHubClient
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default);
    Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default);
}

public interface IHubClientFactory
{
    IHubClient Create(Uri baseAddress);
}