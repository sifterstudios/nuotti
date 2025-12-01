using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
namespace Nuotti.Backend.Tests;

public class StatusEndpointsTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    private readonly WebApplicationFactory<QuizHub> _factory;

    public StatusEndpointsTests(WebApplicationFactory<QuizHub> factory)
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

    [Fact]
    public async Task Status_AfterMultipleCommands_ReturnsLatestSnapshot()
    {
        var session = "status-test-session-2";
        var client = _factory.CreateClient();

        // Start game
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer",
            CommandId = Guid.NewGuid()
        };
        await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);
        await Task.Delay(200);

        // Get status - should be Start
        var status1 = await client.GetAsync($"/status/{session}");
        var snapshot1 = await status1.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot1);
        Assert.Equal(Phase.Start, snapshot1!.Phase);

        // Play song
        var play = new PlaySong(new SongId("song-1"))
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer",
            CommandId = Guid.NewGuid()
        };
        await client.PostAsJsonAsync($"/v1/message/phase/play-song/{session}", play);
        await Task.Delay(200);

        // Get status again - should be Play
        var status2 = await client.GetAsync($"/status/{session}");
        var snapshot2 = await status2.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot2);
        Assert.Equal(Phase.Play, snapshot2!.Phase);
    }
}
