using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.SimKit.Hub;
namespace Nuotti.SimKit.Actors;

public sealed class EngineActor : BaseActor
{
    readonly double _failureRate;
    readonly Random _random;
    readonly List<EngineStatusChanged> _emitted = new();
    readonly List<IDisposable> _subscriptions = [];

    public EngineActor(IHubClientFactory hubClientFactory, Uri baseUri, string session, double failureRate = 0, Random? random = null)
        : base(hubClientFactory, baseUri, session)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (failureRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(failureRate), "Failure rate must be between 0 and 1.");
        _failureRate = failureRate;
        _random = random;
    }

    protected override string Role => "engine";

    /// <summary>
    /// Emitted engine status changes (for testing/inspection).
    /// </summary>
    public IReadOnlyList<EngineStatusChanged> Emitted => _emitted;

    protected override Task OnStartedAsync(CancellationToken cancellationToken = default)
    {
        if (Client is not null)
        {
            _subscriptions.Add(Client.On<PlayTrack>(_ => { OnTrackPlayRequested(); return Task.CompletedTask; }));
            _subscriptions.Add(Client.On<StopTrack>(_ => { OnTrackStopped(); return Task.CompletedTask; }));
        }
        return Task.CompletedTask;
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulate receiving a request to play a track.
    /// On success emits Playing; on failure emits Error.
    /// </summary>
    public void OnTrackPlayRequested()
    {
        var failed = _random.NextDouble() < _failureRate;
        var status = failed ? EngineStatus.Error : EngineStatus.Playing;
        Emit(new EngineStatusChanged(status, 0));
    }

    /// <summary>
    /// Simulate that playback has been stopped; emit Ready.
    /// </summary>
    public void OnTrackStopped()
    {
        Emit(new EngineStatusChanged(EngineStatus.Ready, 0));
    }

    void Emit(EngineStatusChanged evt)
    {
        _emitted.Add(evt);
        // Publishing EngineStatusChanged back to the hub would require a send member on IHubClient, which does not yet exist.
    }
}