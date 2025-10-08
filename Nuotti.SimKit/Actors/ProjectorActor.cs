using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using Nuotti.SimKit.Hub;
namespace Nuotti.SimKit.Actors;

public sealed class ProjectorActor : BaseActor
{
    readonly IReadOnlyList<Phase>? _expectedPhases;
    readonly List<Phase> _receivedPhases = new();
    IDisposable? _subscription;

    public ProjectorActor(IHubClientFactory hubClientFactory, Uri baseUri, string session, IEnumerable<Phase>? expectedPhases = null)
        : base(hubClientFactory, baseUri, session)
    {
        _expectedPhases = expectedPhases?.ToList();
    }

    protected override string Role => "projector";

    public IReadOnlyList<Phase> ReceivedPhases => _receivedPhases;

    public Task OnStateAsync(GameStateSnapshot snapshot)
    {
        if (snapshot is null) return Task.CompletedTask;
        var phase = snapshot.Phase;
        _receivedPhases.Add(phase);

        if (_expectedPhases is { Count: > 0 })
        {
            int index = _receivedPhases.Count - 1;
            if (index < _expectedPhases.Count)
            {
                var expected = _expectedPhases[index];
                if (expected != phase)
                {
                    throw new InvalidOperationException($"Phase assertion failed at index {index}: expected {expected}, got {phase}");
                }
            }
        }
        return Task.CompletedTask;
    }

    protected override Task OnStartedAsync(CancellationToken cancellationToken = default)
    {
        if (Client is not null)
        {
            _subscription = Client.OnGameStateChanged(s => OnStateAsync(s).GetAwaiter().GetResult());
        }
        return Task.CompletedTask;
    }

    protected override Task OnStoppingAsync(CancellationToken cancellationToken = default)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }
}