using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Time;
namespace Nuotti.SimKit.Actors;

/// <summary>
/// Dispatches audience OnStateAsync calls in waves to avoid CPU/network spikes.
/// </summary>
public sealed class AudienceWaveOrchestrator
{
    readonly IReadOnlyList<AudienceActor> _audiences;
    readonly int _waveSize;
    readonly TimeSpan _waveInterval;
    readonly ITimeProvider _time;

    /// <param name="waveSize">Max number of audiences to trigger per wave. <=0 means all at once.</param>
    /// <param name="waveInterval">Delay between waves.</param>
    public AudienceWaveOrchestrator(IEnumerable<AudienceActor> audiences, int waveSize, TimeSpan waveInterval, ITimeProvider? time = null)
    {
        _audiences = audiences.ToList();
        _waveSize = waveSize;
        _waveInterval = waveInterval < TimeSpan.Zero ? TimeSpan.Zero : waveInterval;
        _time = time ?? new RealTimeProvider(1.0);
    }

    public async Task DispatchAsync(GameStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (_audiences.Count == 0) return;
        if (_waveSize <= 0 || _waveSize >= _audiences.Count)
        {
            // single wave
            await Task.WhenAll(_audiences.Select(a => a.OnStateAsync(snapshot, cancellationToken)));
            return;
        }

        for (int offset = 0; offset < _audiences.Count; offset += _waveSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = _audiences.Skip(offset).Take(_waveSize).ToArray();
            await Task.WhenAll(batch.Select(a => a.OnStateAsync(snapshot, cancellationToken)));

            if (offset + _waveSize < _audiences.Count && _waveInterval > TimeSpan.Zero)
            {
                try { await _time.Delay(_waveInterval, cancellationToken); }
                catch (TaskCanceledException) { throw; }
            }
        }
    }
}