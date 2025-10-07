namespace Nuotti.SimKit.Time;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
}
