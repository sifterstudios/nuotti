using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace Nuotti.Backend.Tests;

public class ApiEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task PushQuestion_Performer_Returns202()
    {
        var session = "rest-api-session-1";
        var client = _factory.CreateClient();

        var payload = new QuestionPushed("What?", ["A", "B"]) {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-1"
        };

        var resp = await client.PostAsJsonAsync($"/api/pushQuestion/{session}", payload);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task PushQuestion_WrongRole_Returns403_WithProblem()
    {
        var session = "rest-api-session-2";
        var client = _factory.CreateClient();

        var payload = new QuestionPushed("What?", ["A", "B"]) {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1"
        };

        var resp = await client.PostAsJsonAsync($"/api/pushQuestion/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(403, problem.Status);
        Assert.Equal(ReasonCode.UnauthorizedRole, problem.Reason);
    }

    [Fact]
    public async Task Play_Performer_Returns202()
    {
        var session = "rest-api-session-3";
        var client = _factory.CreateClient();

        var payload = new PlayTrack("https://example.com/track.mp3")
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-2"
        };

        var resp = await client.PostAsJsonAsync($"/api/play/{session}", payload);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Play_WrongRole_Returns403()
    {
        var session = "rest-api-session-4";
        var client = _factory.CreateClient();

        var payload = new PlayTrack("file://local.wav")
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = "aud-2"
        };

        var resp = await client.PostAsJsonAsync($"/api/play/{session}", payload);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Phase_InvalidTransition_Returns409()
    {
        var session = "rest-api-session-5";
        var client = _factory.CreateClient();

        // EndSong from initial Lobby is invalid -> 409
        var resp = await client.PostAsJsonAsync($"/v1/message/phase/end-song/{session}", new EndSong(new SongId("song-1"))
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-3"
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Demo_Unprocessable_Returns422()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/demo/problem/unprocessable");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
