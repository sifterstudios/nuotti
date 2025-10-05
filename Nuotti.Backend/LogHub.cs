using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Event;

namespace Nuotti.Backend;

public class LogHub : Hub
{
    // Allow clients (Audience/Projector/TestClient) to publish log events that will be
    // fanned out to all connected log listeners (e.g., the Projector UI).
    public Task Publish(LogEvent e)
        => Clients.All.SendAsync("Log", e);
}

public interface ILogStreamer
{
    Task BroadcastAsync(LogEvent e);
}

public class LogStreamer(IHubContext<LogHub> hub) : ILogStreamer
{
    public Task BroadcastAsync(LogEvent e)
        => hub.Clients.All.SendAsync("Log", e);
}
