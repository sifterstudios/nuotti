using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
namespace Nuotti.Backend;

[ApiController]
[Route("[controller]")]
public class TestController(IHubContext<GameHub> hubContext) : ControllerBase
{
    [HttpGet("broadcast/{message}")]
    public async Task<IActionResult> BroadcastMessage(string message)
    {
        Debug.WriteLine($"Broadcasting message: {message}");
        await hubContext.Clients.All.SendAsync("ReceiveSongUpdate", message);
        return Ok($"Sent: {message}");
    }
}