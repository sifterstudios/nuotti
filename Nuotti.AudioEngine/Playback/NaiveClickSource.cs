using System.Timers;
using Timer = System.Timers.Timer;
namespace Nuotti.AudioEngine.Playback;

/// <summary>
/// Simple timer-based click generator stub. Does not produce actual audio yet, only models timing/accent logic.
/// </summary>
public sealed class NaiveClickSource : IClickSource, IDisposable
{
    private readonly ClickOptions _options;
    private readonly int _accentEvery;
    private readonly bool _hasRouting;
    private Timer? _timer;
    private int _beat;

    public NaiveClickSource(ClickOptions options, bool hasRouting, int accentEvery = 4)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _accentEvery = accentEvery <= 0 ? 4 : accentEvery;
        _hasRouting = hasRouting;
    }

    public bool Enabled => _hasRouting && _options.Level > 0;

    public static TimeSpan IntervalFromBpm(int bpm)
    {
        if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
        return TimeSpan.FromMinutes(1.0 / bpm);
    }

    public void Start()
    {
        if (!Enabled) return;
        Stop(); // ensure clean
        _beat = 0;
        _timer = new Timer(IntervalFromBpm(_options.Bpm).TotalMilliseconds)
        {
            AutoReset = true,
            Enabled = true
        };
        _timer.Elapsed += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        _beat++;
        var accented = (_beat % _accentEvery) == 1; // 1,5,9..
        // TODO: produce audio on click bus based on accent and _options.Level.
        // For stub, do nothing.
        if (_beat > 1_000_000) _beat = (_beat % _accentEvery); // avoid overflow in long runs
    }

    public void Stop()
    {
        if (_timer is null) return;
        try
        {
            _timer.Elapsed -= OnTick;
            _timer.Stop();
            _timer.Dispose();
        }
        finally
        {
            _timer = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
