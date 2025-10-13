using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.SimKit.Hub;
namespace Nuotti.SimKit.Actors;

public sealed class EngineActor : BaseActor
{
    readonly double _failureRate;
    readonly Random _random;
    readonly List<EngineStatusChanged> _emitted = new();

    public EngineActor(IHubClientFactory hubClientFactory, Uri baseUri, string session, double failureRate = 0, Random? random = null)
        : base(hubClientFactory, baseUri, session)
    {
        if (failureRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(failureRate), "Failure rate must be between 0 and 1.");
        _failureRate = failureRate;
        _random = random ?? new Random();
    }

    protected override string Role => "engine";

    /// <summary>
    /// Emitted engine status changes (for testing/inspection).
    /// </summary>
    public IReadOnlyList<EngineStatusChanged> Emitted => _emitted;

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
        // In a real implementation, this would publish to the hub.
    }
}