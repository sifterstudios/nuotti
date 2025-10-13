using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using Xunit;
namespace Nuotti.Backend.Tests;

public class CorsAndSignalRTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { /* ensure default Development env */ });

    [Fact]
    public async Task Dev_Preflight_Allows_Localhost_Origin_And_Credentials()
    {
        var client = _factory.CreateClient();

        var origin = "http://localhost:3000";
        using var req = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "GET");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode); // preflight
        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin));
        Assert.Contains(origin, allowOrigin);
        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Credentials", out var allowCreds));
        Assert.Contains("true", allowCreds);
    }

    [Fact]
    public async Task Dev_SignalR_Hub_Allows_Connection_From_Localhost_Origin()
    {
        // Arrange TestServer handler and base URL
        var handler = _factory.Server.CreateHandler();
        var baseAddress = _factory.Server.BaseAddress;

        // Create a delegating handler to inject the Origin header
        var origin = "http://localhost:5173";
        var injectingHandler = new OriginInjectingHandler(origin, handler);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseAddress, "/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => injectingHandler;
            })
            .WithAutomaticReconnect()
            .Build();

        // Act + Assert: should connect without CORS errors
        await connection.StartAsync();
        await connection.DisposeAsync();
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
