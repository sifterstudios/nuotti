namespace Nuotti.SimKit.Time;

public sealed class ImmediateTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
