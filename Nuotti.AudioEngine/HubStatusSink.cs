using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.AudioEngine;

internal sealed class HubStatusSink : IEngineStatusSink
{
    private readonly HubConnection _hub;
    private readonly string _session;

    public HubStatusSink(HubConnection hub, string session)
    {
        _hub = hub;
        _session = session;
    }

    public Task PublishAsync(EngineStatusChanged evt, CancellationToken cancellationToken = default)
        => _hub.InvokeAsync("EngineStatusChanged", _session, evt, cancellationToken);
}
