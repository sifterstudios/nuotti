using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
namespace Nuotti.Backend.Tests;

public class BackendCommandFlowTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public BackendCommandFlowTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    static StartGame MakeStart(string session) => new StartGame
    {
        SessionCode = session,
        IssuedByRole = Role.Performer,
        IssuedById = "test-performer"
    };

    [Fact]
    public async Task StartGame_HappyPath_Returns202_AndBroadcasts()
    {
        var session = "test-session-1";
        var client = _factory.CreateClient();

        // Prepare SignalR connection using TestServer handler
        var handler = _factory.Server.CreateHandler();
        var baseAddress = _factory.Server.BaseAddress;
        var tcs = new TaskCompletionSource<GameStateSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseAddress, "/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            if (snapshot.SessionCode == session)
                tcs.TrySetResult(snapshot);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("Join", session, "projector", null);

        // Send StartGame without explicit session creation; server initializes state lazily
        var resp = await client.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", MakeStart(session));
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        // Wait for broadcast
        var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(received, tcs.Task), "Did not receive GameStateChanged in time");
        var snapshot = await tcs.Task;
        Assert.Equal(Phase.Start, snapshot.Phase);
        Assert.Equal(session, snapshot.SessionCode);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task InvalidTransition_Returns409_WithReason()
    {
        var session = "test-session-2";
        var client = _factory.CreateClient();

        // Attempt to EndSong in initial phase (Lobby) -> invalid transition
        var endResp = await client.PostAsJsonAsync($"/v1/message/phase/end-song/{session}", new EndSong(new SongId(Guid.NewGuid().ToString()))
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        });

        Assert.Equal(HttpStatusCode.Conflict, endResp.StatusCode);
        var problem = await endResp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(409, problem!.Status);
        Assert.Equal(ReasonCode.InvalidStateTransition, problem.Reason);
    }
}
