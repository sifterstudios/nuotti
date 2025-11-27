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

public class ApiEndpointsTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { });

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

    [Fact]
    public async Task Stop_Performer_Returns202()
    {
        var session = "rest-api-session-6";
        var client = _factory.CreateClient();

        var payload = new StopTrack
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-4",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/api/stop/{session}", payload);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task CreateSession_Performer_Returns202()
    {
        var session = "rest-api-session-7";
        var client = _factory.CreateClient();

        var payload = new CreateSession(session)
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-5",
            CommandId = Guid.NewGuid()
        };

        var resp = await client.PostAsJsonAsync($"/v1/message/phase/create-session/{session}", payload);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Sessions_Create_Returns200_WithSessionCreated()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/sessions/test-session", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        var session = await resp.Content.ReadFromJsonAsync<SessionCreated>(ContractsJson.RestOptions);
        Assert.NotEqual(default(SessionCreated), session);
        Assert.NotNull(session.SessionCode);
        Assert.NotNull(session.HostId);
    }

    [Fact]
    public Task Sessions_Create_Payload_Snapshot()
    {
        var session = new SessionCreated("test-session", "host-123");
        var json = System.Text.Json.JsonSerializer.Serialize(session, ContractsJson.RestOptions);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public async Task Sessions_Counts_Returns200_WithCounts()
    {
        var session = "rest-api-session-8";
        var client = _factory.CreateClient();

        var resp = await client.GetAsync($"/api/sessions/{session}/counts");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        var json = await resp.Content.ReadAsStringAsync();
        Assert.NotNull(json);
        Assert.Contains("performer", json);
        Assert.Contains("projector", json);
        Assert.Contains("engine", json);
        Assert.Contains("audiences", json);
    }

    [Fact]
    public Task Sessions_Counts_Payload_Snapshot()
    {
        var counts = new { performer = 1, projector = 1, engine = 0, audiences = 5 };
        var json = System.Text.Json.JsonSerializer.Serialize(counts, ContractsJson.RestOptions);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public async Task Status_ReturnsLatestSnapshot()
    {
        var session = "rest-api-session-9";
        var client = _factory.CreateClient();

        // Create session and start game
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "perf-6",
            CommandId = Guid.NewGuid()
        };
        await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);

        // Wait a bit for state to update
        await Task.Delay(200);

        // Get status
        var resp = await client.GetAsync($"/status/{session}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        var snapshot = await resp.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot);
        Assert.Equal(session, snapshot!.SessionCode);
        Assert.Equal(Phase.Start, snapshot.Phase);
    }

    [Fact]
    public Task Status_Snapshot_Payload_Snapshot()
    {
        var snapshot = new GameStateSnapshot(
            sessionCode: "TEST-SESSION",
            phase: Phase.Guessing,
            songIndex: 1,
            currentSong: new SongRef(new SongId("song-1"), "Test Song", "Test Artist"),
            catalog: [new SongRef(new SongId("song-1"), "Test Song", "Test Artist")],
            choices: ["Option A", "Option B", "Option C"],
            hintIndex: 0,
            tallies: [5, 3, 2],
            scores: new Dictionary<string, int> { ["player-1"] = 10, ["player-2"] = 8 }
        );
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot, ContractsJson.RestOptions);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public async Task Health_Live_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("live", json);
    }

    [Fact]
    public Task Health_Live_Payload_Snapshot()
    {
        var health = new { status = "live" };
        var json = System.Text.Json.JsonSerializer.Serialize(health, ContractsJson.RestOptions);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public async Task Health_Ready_Returns200()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("ready", json);
    }

    [Fact]
    public Task Health_Ready_Payload_Snapshot()
    {
        var health = new { status = "ready" };
        var json = System.Text.Json.JsonSerializer.Serialize(health, ContractsJson.RestOptions);
        return Verifier.VerifyJson(json);
    }

    [Fact]
    public async Task Manifest_Performer_Returns202()
    {
        var session = "rest-api-session-10";
        var client = _factory.CreateClient();

        var manifest = new SetlistManifest
        {
            Songs = new List<SetlistManifest.SongEntry>
            {
                new SetlistManifest.SongEntry { Title = "Song 1", Artist = "Artist 1", File = "file://test1.mp3" },
                new SetlistManifest.SongEntry { Title = "Song 2", Artist = "Artist 2", File = "file://test2.mp3" }
            }
        };

        var resp = await client.PostAsJsonAsync($"/api/manifest/{session}", manifest);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }
}
