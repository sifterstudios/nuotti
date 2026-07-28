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
    /// <remarks>
    /// The handler returns a Task so the publisher can await it. With Action&lt;T&gt;, any handler
    /// that awaited was an async void: receive order was unguaranteed and exceptions were
    /// unobservable, which makes a recorded run irreproducible.
    /// </remarks>
    IDisposable OnGameStateChanged(Func<GameStateSnapshot, Task> handler);
}

public interface IHubClientFactory
{
    IHubClient Create(Uri baseAddress);
}