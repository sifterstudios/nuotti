using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit.Hub;

public interface IHubClient
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task JoinAsync(string session, string role, string? name = null, CancellationToken cancellationToken = default);
    Task SubmitAnswerAsync(string session, int choiceIndex, CancellationToken cancellationToken = default);
    /// <summary>
    /// Subscribe to GameStateChanged broadcast from the hub.
    /// Returns IDisposable to allow unsubscription.
    /// </summary>
    IDisposable OnGameStateChanged(Action<GameStateSnapshot> handler);
}

public interface IHubClientFactory
{
    IHubClient Create(Uri baseAddress);
}