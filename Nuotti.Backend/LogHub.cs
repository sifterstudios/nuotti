using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Event;

namespace Nuotti.Backend;

public class LogHub : Hub
{
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
