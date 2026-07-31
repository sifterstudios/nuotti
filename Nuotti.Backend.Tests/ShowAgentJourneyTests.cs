using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Backend.Tests;

public sealed class ShowAgentJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Pair_status_poll_and_revoke_preserve_current_render_but_block_new_commands()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client);
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Venue band" });
        await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspace.WorkspaceId}/select", owner.SessionToken);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/VENUE1/create", owner.SessionToken)).EnsureSuccessStatusCode();

        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/VENUE1/show-agent/pairings", owner.SessionToken, null);
        Assert.Matches("^[0-9]{8}$", pairing.Code);
        var paired = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Stage laptop" });

        var statusReport = await SendAsync(client, HttpMethod.Put, "/v1/show-agent/status", paired.AccessToken,
            new { state = "Playing", detail = "current-track.wav" });
        Assert.Equal(HttpStatusCode.NoContent, statusReport.StatusCode);

        var store = _factory.Services.GetRequiredService<IShowAgentAccessStore>();
        await store.AppendCommandAsync(workspace.WorkspaceId, "VENUE1", "PlayTrack", new PlayTrack("next-track.wav")
        {
            SessionCode = "VENUE1",
            IssuedById = owner.Principal.UserId,
            IssuedByRole = Nuotti.Contracts.V1.Enum.Role.Performer
        });
        var commands = await GetAsync<ShowAgentCommand[]>(client, "/v1/show-agent/commands?after=0", paired.AccessToken);
        Assert.Single(commands);
        Assert.Equal("PlayTrack", commands[0].MessageType);

        var revoke = await SendAsync(client, HttpMethod.Delete,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/VENUE1/show-agent", owner.SessionToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var blockedPoll = await SendAsync(client, HttpMethod.Get, "/v1/show-agent/commands?after=1", paired.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, blockedPoll.StatusCode);
        var blockedRefresh = await SendAsync(client, HttpMethod.Post, "/v1/show-agent/token", null,
            new { credential = paired.Credential });
        Assert.Equal(HttpStatusCode.Unauthorized, blockedRefresh.StatusCode);

        var status = await GetAsync<ShowAgentStatus>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/VENUE1/show-agent", owner.SessionToken);
        Assert.True(status.Revoked);
        Assert.Equal(ShowAgentConnectionState.Playing, status.State);
        Assert.Equal("current-track.wav", status.Detail);

        var replacementCode = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/VENUE1/show-agent/pairings", owner.SessionToken, null);
        var replacement = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = replacementCode.Code, name = "Replacement laptop" });
        var replacementCommands = await GetAsync<ShowAgentCommand[]>(client,
            "/v1/show-agent/commands?after=0", replacement.AccessToken);
        Assert.Empty(replacementCommands);
        await store.AppendCommandAsync(workspace.WorkspaceId, "VENUE1", "StopTrack", new { });
        var newCommands = await GetAsync<ShowAgentCommand[]>(client,
            "/v1/show-agent/commands?after=0", replacement.AccessToken);
        Assert.Single(newCommands);
        Assert.Equal(2, newCommands[0].Sequence);
    }

    [Fact]
    public async Task Malformed_credential_is_rejected_and_pairing_attempts_are_throttled()
    {
        await using var isolatedFactory = baseFactory.WithWebHostBuilder(_ => { });
        using var client = isolatedFactory.CreateClient();
        var malformed = await SendAsync(client, HttpMethod.Post, "/v1/show-agent/token", null,
            new { credential = "" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var invalid = await SendAsync(client, HttpMethod.Post, "/v1/show-agent/pair", null,
                new { code = "99999999", name = "Guesser" });
            Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        }
        var throttled = await SendAsync(client, HttpMethod.Post, "/v1/show-agent/pair", null,
            new { code = "99999999", name = "Guesser" });
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task Pairing_code_is_single_use_and_agent_has_no_workspace_bearer_access()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client);
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Isolation band" });
        await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspace.WorkspaceId}/select", owner.SessionToken);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/ISO1/create", owner.SessionToken)).EnsureSuccessStatusCode();
        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/ISO1/show-agent/pairings", owner.SessionToken, null);
        var paired = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Agent" });

        var reuse = await SendAsync(client, HttpMethod.Post, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Attacker" });
        Assert.Equal(HttpStatusCode.NotFound, reuse.StatusCode);
        var workspaceAccess = await SendAsync(client, HttpMethod.Get, "/v1/workspaces", paired.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, workspaceAccess.StatusCode);
    }

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client)
    {
        var email = $"agent-{Guid.NewGuid():N}@example.test";
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null, new { email });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = link.Token });
    }

    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<T> GetAsync<T>(HttpClient client, string path, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, path, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
