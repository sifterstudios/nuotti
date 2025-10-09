using Microsoft.AspNetCore.Mvc.Testing;
using System.Collections.Concurrent;
using Xunit;
namespace Nuotti.Performer.Tests;

public class PerformerClientSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { /* default dev env */ });

    [Fact]
    public async Task Disconnect_Reconnect_restores_indicator()
    {
        var handler = _factory.Server.CreateHandler();
        var baseAddress = _factory.Server.BaseAddress;

        // Inject Origin header similar to browser
        var origin = "http://localhost:5173";

        await using var client = new PerformerClient(baseAddress, sessionCode: "dev")
        {
            HttpMessageHandlerDecorator = _ => new OriginInjectingHandler(origin, _factory.Server.CreateHandler())
        };

        var events = new ConcurrentQueue<bool>();
        client.ConnectedChanged += connected => events.Enqueue(connected);

        await client.EnsureConnectedAsync();
        await Task.Delay(50);
        Assert.True(client.IsConnected);
        Assert.Contains(true, events);

        await client.DisconnectAsync();
        await Task.Delay(50);
        Assert.False(client.IsConnected);
        Assert.Contains(false, events);

        await client.EnsureConnectedAsync();
        await Task.Delay(50);
        Assert.True(client.IsConnected);
        Assert.True(events.Contains(true));
    }

    sealed class OriginInjectingHandler(string origin, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Headers.Contains("Origin"))
            {
                request.Headers.Add("Origin", origin);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

}
