using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Nuotti.Backend.Tests;

public class StatusEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StatusEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Status_UnknownSession_Returns404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/status/unknown-session-xyz");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Status_AfterCommands_ReflectsReducerOutput()
    {
        var session = "status-test-session-1";
        var client = _factory.CreateClient();

        // Send StartGame command; server should initialize state lazily if missing
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        };
        var cmdResp = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);
        Assert.Equal(HttpStatusCode.Accepted, cmdResp.StatusCode);

        // Query status
        var statusResp = await client.GetAsync($"/status/{session}");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        var snapshot = await statusResp.Content.ReadFromJsonAsync<GameStateSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(session, snapshot!.SessionCode);
        Assert.Equal(Phase.Start, snapshot.Phase);
    }
}
