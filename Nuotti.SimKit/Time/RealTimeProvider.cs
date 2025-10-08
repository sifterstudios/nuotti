namespace Nuotti.SimKit.Time;

public sealed class RealTimeProvider : ITimeProvider
{
    public double Speed { get; }

    public RealTimeProvider(double speed = 1.0)
    {
        Speed = speed <= 0 ? 0 : speed;
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (delay <= TimeSpan.Zero || Speed == 0)
            return Task.CompletedTask;

        // Speed > 1.0 makes delays shorter (faster), Speed < 1.0 slower
        var scaled = TimeSpan.FromMilliseconds(delay.TotalMilliseconds / Speed);
        if (scaled <= TimeSpan.Zero) return Task.CompletedTask;
        return Task.Delay(scaled, cancellationToken);
    }
}
