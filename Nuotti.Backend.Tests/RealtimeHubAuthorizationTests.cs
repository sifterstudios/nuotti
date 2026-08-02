using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend.Realtime;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nuotti.Backend.Tests;

/// <summary>
/// The hub as a deployed environment sees it: credentials required, one path, capabilities derived.
/// </summary>
/// <remarks>
/// The rest of the suite runs with <c>AllowUnauthenticatedConnections</c> on, because the local
/// loop still has clients that predate credentials. These tests turn it off, which is the only
/// configuration that actually ships, so the production surface is exercised rather than assumed.
/// </remarks>
public sealed class RealtimeHubAuthorizationTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    // Post-configure rather than configuration, so the override lands after the Development
    // appsettings file has been bound and cannot be reordered out from under the test.
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
            services.PostConfigure<RealtimeOptions>(o => o.AllowUnauthenticatedConnections = false)));

    [Fact]
    public async Task A_connection_with_no_credential_is_refused()
    {
        // This is the whole reason QuizHub could never be mapped outside Development.
        await using var connection = Connect("/hub?session=NOCRED");
        var closed = WatchForClose(connection);

        await connection.StartAsync();

        Assert.True(await closed, "The hub kept an anonymous connection alive.");
    }

    [Fact]
    public async Task A_stolen_session_code_alone_is_not_enough()
    {
        // Session codes are short enough to shoulder-surf or guess, so possessing one has to buy
        // nothing on its own. The audience path issues a real token; this one has not walked it.
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client, "eaves");

        await using var connection = Connect($"/hub?session={band.SessionCode}");
        var closed = WatchForClose(connection);

        await connection.StartAsync();

        Assert.True(await closed, "A bare session code was enough to reach the hub.");
    }

    [Fact]
    public async Task An_audience_member_who_joined_may_connect_and_answer()
    {
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client, "fan");
        var ticket = await JoinAsync(client, band.SessionCode);

        await using var connection = Connect($"/hub?session={band.SessionCode}&access_token={ticket.Token}");
        var problem = Capture(connection);
        await connection.StartAsync();
        await connection.InvokeAsync("SubmitAnswer", band.SessionCode, 0, Guid.NewGuid());

        // The session has not opened answers yet, so this is refused - but by the game rules, not
        // by the capability check. Anything about roles here would mean the audience lost its own
        // core ability.
        Assert.DoesNotContain("audience member", await problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_paired_projector_may_watch_but_may_not_answer()
    {
        // A device on a venue's network is the least trusted thing in the system: it renders what
        // it is told. If it could answer, it could also stuff the vote.
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client, "venue");
        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{band.WorkspaceId}/sessions/{band.SessionCode}/show-agent/pairings", band.SessionToken, null);
        var device = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Stage projector" });

        await using var connection = Connect(
            $"/hub?session={band.SessionCode}&deviceRole=projector&access_token={device.AccessToken}");
        var problem = Capture(connection);
        await connection.StartAsync();
        await connection.InvokeAsync("SubmitAnswer", band.SessionCode, 0, Guid.NewGuid());

        Assert.Contains("audience member", await problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_workspace_member_may_connect_to_their_own_session()
    {
        using var client = _factory.CreateClient();
        var band = await StartSessionAsync(client, "performer");

        await using var connection = Connect(
            $"/hub?sessionCode={band.SessionCode}&workspaceId={band.WorkspaceId}&access_token={band.SessionToken}");
        var closed = WatchForClose(connection);

        await connection.StartAsync();

        Assert.False(await closed, "The hub dropped a signed-in member of the workspace that owns the session.");
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    /// <summary>
    /// True when the hub hung up on this connection.
    /// </summary>
    /// <remarks>
    /// StartAsync is not the signal. SignalR replies to the handshake before OnConnectedAsync
    /// runs, so a refused connection still sees StartAsync succeed and only then gets dropped -
    /// which is exactly the shape of "it looks connected but nothing ever arrives".
    /// </remarks>
    static Task<bool> WatchForClose(HubConnection connection)
    {
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult(true);
            return Task.CompletedTask;
        };
        return Settle(closed.Task);

        static async Task<bool> Settle(Task<bool> task)
            => await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))) == task && await task;
    }

    HubConnection Connect(string pathAndQuery) => new HubConnectionBuilder()
        .WithUrl(new Uri(_factory.Server.BaseAddress, pathAndQuery),
            options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
        .Build();

    /// <summary>
    /// The first Problem the hub pushes back, or empty if none arrives.
    /// </summary>
    /// <remarks>
    /// InvokeAsync returns when the hub method returns, which is not when its Clients.Caller send
    /// has reached the client. Collecting into a list and asserting immediately is a race that
    /// passes most of the time, which is worse than failing.
    /// </remarks>
    static Task<string> Capture(HubConnection connection)
    {
        var arrived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<System.Text.Json.JsonElement>("Problem", problem => arrived.TrySetResult(problem.ToString()));
        return WaitAsync(arrived.Task);

        static async Task<string> WaitAsync(Task<string> task)
        {
            var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            return finished == task ? await task : string.Empty;
        }
    }

    sealed record Band(string SessionToken, string WorkspaceId, string SessionCode);

    static async Task<Band> StartSessionAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        var member = await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null,
            new { token = link.Token });
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "The Satellites" });
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspace.WorkspaceId}/select",
            member.SessionToken)).EnsureSuccessStatusCode();
        var sessionCode = $"S{Guid.NewGuid():N}"[..6].ToUpperInvariant();
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{sessionCode}/create",
            member.SessionToken)).EnsureSuccessStatusCode();
        return new Band(member.SessionToken, workspace.WorkspaceId, sessionCode);
    }

    sealed record JoinedAudienceTicket(string ParticipantId, string SessionCode, string Token);

    static Task<JoinedAudienceTicket> JoinAsync(HttpClient client, string sessionCode)
        => PostAsync<JoinedAudienceTicket>(client, $"/v1/sessions/{sessionCode}/join", null,
            new { deviceSecret = Guid.NewGuid().ToString("N") });

    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
