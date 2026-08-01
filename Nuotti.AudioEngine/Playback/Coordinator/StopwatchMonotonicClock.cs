using System.Diagnostics;

namespace Nuotti.AudioEngine.Playback.Coordinator;

/// <summary>Production monotonic clock backed by <see cref="Stopwatch"/>.</summary>
public sealed class StopwatchMonotonicClock : IMonotonicClock
{
    readonly Stopwatch _sw = Stopwatch.StartNew();

    public TimeSpan Elapsed => _sw.Elapsed;

    public void Advance(TimeSpan delta) =>
        throw new InvalidOperationException("Production clock cannot be advanced; use FakeMonotonicClock in tests.");
}
