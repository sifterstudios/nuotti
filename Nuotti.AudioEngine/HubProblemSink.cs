using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.AudioEngine;

internal sealed class HubProblemSink : IProblemSink
{
    private readonly HubConnection _hub;
    private readonly string _session;

    public HubProblemSink(HubConnection hub, string session)
    {
        _hub = hub;
        _session = session;
    }

    public Task PublishAsync(NuottiProblem problem, CancellationToken cancellationToken = default)
        => _hub.InvokeAsync("Problem", _session, problem, cancellationToken);
}
