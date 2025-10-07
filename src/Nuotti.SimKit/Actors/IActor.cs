namespace Nuotti.SimKit.Actors;

public interface IActor
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}