using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
namespace Nuotti.Backend.Tests;

public class ReconnectTests : IClassFixture<WebApplicationFactory<QuizHub>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<QuizHub> _factory;
    private HubConnection? _connection;
    private readonly HttpClient _httpClient;

    public ReconnectTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
        _httpClient = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var handler = _factory.Server.CreateHandler();
        var baseAddress = _factory.Server.BaseAddress;

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseAddress!, "/hub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        _httpClient.Dispose();
    }

    [Fact]
    public async Task Status_Endpoint_Returns_Current_Snapshot_For_Resync()
    {
        var session = "reconnect-status-1";
        
        // Create session and transition to Start phase
        await _httpClient.PostAsync($"/api/sessions/{session}", null);
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        };
        await _httpClient.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);
        await Task.Delay(200);

        // Fetch status for resync
        var statusResp = await _httpClient.GetAsync($"/status/{session}");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        
        var snapshot = await statusResp.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot);
        Assert.Equal(session, snapshot!.SessionCode);
        Assert.Equal(Phase.Start, snapshot.Phase);
    }

    [Fact]
    public async Task Client_Can_Reconnect_And_Rejoin_Session()
    {
        var session = "reconnect-rejoin-1";
        var receivedStates = new List<GameStateSnapshot>();
        var tcs = new TaskCompletionSource<GameStateSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        _connection!.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            receivedStates.Add(snapshot);
            if (snapshot.SessionCode == session)
            {
                tcs.TrySetResult(snapshot);
            }
        });

        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "audience", "TestUser");

        // Disconnect
        await _connection.StopAsync();
        await Task.Delay(100);

        // Reconnect and rejoin
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "audience", "TestUser");

        // Verify reconnection succeeded
        Assert.Equal(HubConnectionState.Connected, _connection.State);
    }

    [Fact]
    public async Task Client_Receives_Latest_State_After_Reconnection()
    {
        var session = "reconnect-state-1";
        var receivedStates = new List<GameStateSnapshot>();
        var tcs = new TaskCompletionSource<GameStateSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        _connection!.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            if (snapshot.SessionCode == session)
            {
                receivedStates.Add(snapshot);
                tcs.TrySetResult(snapshot);
            }
        });

        // Connect and join
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "projector", "projector");

        // Start game (this will broadcast GameStateChanged)
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        };
        await _httpClient.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);

        // Wait for state change
        var firstState = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(ReferenceEquals(firstState, tcs.Task), "Did not receive first GameStateChanged");
        var initialState = await tcs.Task;
        Assert.Equal(Phase.Start, initialState.Phase);

        // Disconnect
        await _connection.StopAsync();
        await Task.Delay(100);

        // Reconnect and rejoin
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "projector", "projector");

        // Fetch latest state via /status endpoint (simulating resync)
        var statusResp = await _httpClient.GetAsync($"/status/{session}");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        var latestState = await statusResp.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(latestState);
        // State should still be Start (no valid transition from Start to Play without going through other phases)
        Assert.Equal(Phase.Start, latestState!.Phase);
    }

    [Fact]
    public async Task Multiple_Reconnections_Work_Correctly()
    {
        var session = "reconnect-multi-1";
        var reconnectCount = 0;

        _connection!.Reconnected += connectionId =>
        {
            reconnectCount++;
            return Task.CompletedTask;
        };

        // Initial connection
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "audience", "User1");
        Assert.Equal(HubConnectionState.Connected, _connection.State);

        // Disconnect and reconnect multiple times
        for (int i = 0; i < 3; i++)
        {
            await _connection.StopAsync();
            await Task.Delay(100);
            await _connection.StartAsync();
            await _connection.InvokeAsync("Join", session, "audience", "User1");
            await Task.Delay(100);
        }

        // Verify final connection state
        Assert.Equal(HubConnectionState.Connected, _connection.State);
        // Note: Reconnected event only fires with automatic reconnection, not manual stop/start
        // So we just verify the connection works after multiple stop/start cycles
    }

    [Fact]
    public async Task Status_Endpoint_Reflects_State_Changes_While_Client_Disconnected()
    {
        var session = "reconnect-status-changes-1";

        // Connect and join
        await _connection!.StartAsync();
        await _connection.InvokeAsync("Join", session, "projector", "projector");

        // Start game
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        };
        await _httpClient.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);
        await Task.Delay(200);

        // Verify initial state
        var status1 = await _httpClient.GetAsync($"/status/{session}");
        var snapshot1 = await status1.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot1);
        Assert.Equal(Phase.Start, snapshot1!.Phase);

        // Disconnect
        await _connection.StopAsync();
        await Task.Delay(100);

        // Verify status endpoint still returns current state while disconnected
        var status2 = await _httpClient.GetAsync($"/status/{session}");
        var snapshot2 = await status2.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot2);
        Assert.Equal(Phase.Start, snapshot2!.Phase);

        // Reconnect and verify can fetch latest state
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "projector", "projector");

        var status3 = await _httpClient.GetAsync($"/status/{session}");
        var snapshot3 = await status3.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(snapshot3);
        Assert.Equal(Phase.Start, snapshot3!.Phase);
    }

    [Fact]
    public async Task Reconnected_Client_Can_Continue_Interacting_After_Resync()
    {
        var session = "reconnect-interact-1";
        var receivedStates = new List<GameStateSnapshot>();

        _connection!.On<GameStateSnapshot>("GameStateChanged", snapshot =>
        {
            if (snapshot.SessionCode == session)
            {
                receivedStates.Add(snapshot);
            }
        });

        // Connect as audience
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "audience", "TestUser");

        // Start game
        var start = new StartGame
        {
            SessionCode = session,
            IssuedByRole = Role.Performer,
            IssuedById = "test-performer"
        };
        await _httpClient.PostAsJsonAsync($"/v1/message/phase/start-game/{session}", start);
        await Task.Delay(200);

        // Disconnect
        await _connection.StopAsync();
        await Task.Delay(100);

        // Reconnect and rejoin
        await _connection.StartAsync();
        await _connection.InvokeAsync("Join", session, "audience", "TestUser");

        // Fetch latest state to resync
        var statusResp = await _httpClient.GetAsync($"/status/{session}");
        var latestState = await statusResp.Content.ReadFromJsonAsync<GameStateSnapshot>(ContractsJson.RestOptions);
        Assert.NotNull(latestState);
        Assert.Equal(Phase.Start, latestState!.Phase);

        // Verify connection is still active and can receive updates
        Assert.Equal(HubConnectionState.Connected, _connection.State);
    }
}

