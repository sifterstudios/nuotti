using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class WorkspaceJourneyTests(WebApplicationFactory<QuizHub> factory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly HttpClient _client = factory.WithWebHostBuilder(_ => { }).CreateClient();

    [Fact]
    public async Task Owner_and_member_can_run_an_isolated_session_journey()
    {
        var owner = await SignInAsync(UniqueEmail("owner"));
        var workspace = await CreateWorkspaceAsync(owner.SessionToken, "The Satellites");
        await SelectAsync(owner.SessionToken, workspace.WorkspaceId);

        var invitation = await SendAsync<IssuedMagicLink>(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/invitations", owner.SessionToken,
            new { email = UniqueEmail("member") });
        var member = await RedeemAsync(invitation.Token);
        await SelectAsync(member.SessionToken, workspace.WorkspaceId);

        var create = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SHOW42/create", owner.SessionToken);
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);

        var start = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SHOW42/start", member.SessionToken);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);

        var recovery = await SendRawAsync(HttpMethod.Get,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SHOW42/recovery", member.SessionToken);
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
    }

    [Fact]
    public async Task Selected_workspace_is_required_and_cross_workspace_access_is_indistinguishable()
    {
        var user = await SignInAsync(UniqueEmail("multi"));
        var first = await CreateWorkspaceAsync(user.SessionToken, "First band");
        var second = await CreateWorkspaceAsync(user.SessionToken, "Second band");

        await SelectAsync(user.SessionToken, first.WorkspaceId);
        var wrongSelection = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{second.WorkspaceId}/sessions/HIDDEN/create", user.SessionToken);
        var nonexistent = await SendRawAsync(HttpMethod.Post,
            "/v1/workspaces/ws_does_not_exist/sessions/HIDDEN/create", user.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, wrongSelection.StatusCode);
        Assert.Equal(nonexistent.StatusCode, wrongSelection.StatusCode);

        await SelectAsync(user.SessionToken, second.WorkspaceId);
        var secondCreate = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{second.WorkspaceId}/sessions/HIDDEN/create", user.SessionToken);
        Assert.Equal(HttpStatusCode.Accepted, secondCreate.StatusCode);

        await SelectAsync(user.SessionToken, first.WorkspaceId);
        var hiddenRecovery = await SendRawAsync(HttpMethod.Get,
            $"/v1/workspaces/{second.WorkspaceId}/sessions/HIDDEN/recovery", user.SessionToken);
        var missingRecovery = await SendRawAsync(HttpMethod.Get,
            $"/v1/workspaces/{second.WorkspaceId}/sessions/MISSING/recovery", user.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, hiddenRecovery.StatusCode);
        Assert.Equal(missingRecovery.StatusCode, hiddenRecovery.StatusCode);
    }

    [Fact]
    public async Task Owner_can_revoke_member_immediately()
    {
        var owner = await SignInAsync(UniqueEmail("revoke-owner"));
        var workspace = await CreateWorkspaceAsync(owner.SessionToken, "Revocation band");
        await SelectAsync(owner.SessionToken, workspace.WorkspaceId);
        var invitation = await SendAsync<IssuedMagicLink>(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/invitations", owner.SessionToken,
            new { email = UniqueEmail("revoke-member") });
        var member = await RedeemAsync(invitation.Token);
        await SelectAsync(member.SessionToken, workspace.WorkspaceId);

        var revoke = await SendRawAsync(HttpMethod.Delete,
            $"/v1/workspaces/{workspace.WorkspaceId}/members/{member.Principal.UserId}", owner.SessionToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var afterRevocation = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/NOPE/create", member.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task Invalid_identity_and_workspace_inputs_are_expected_client_errors()
    {
        var invalidEmail = await SendRawAsync(HttpMethod.Post, "/v1/auth/magic-links", null,
            new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        var owner = await SignInAsync(UniqueEmail("validation"));
        var invalidName = await SendRawAsync(HttpMethod.Post, "/v1/workspaces", owner.SessionToken,
            new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, invalidName.StatusCode);
    }

    [Fact]
    public async Task Invitation_requires_explicit_selection_and_roles_are_strings()
    {
        var owner = await SignInAsync(UniqueEmail("explicit-owner"));
        var workspace = await CreateWorkspaceAsync(owner.SessionToken, "Explicit band");
        Assert.Equal("Owner", JsonSerializer.SerializeToElement(
            workspace, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("role").GetString());
        await SelectAsync(owner.SessionToken, workspace.WorkspaceId);
        var invitation = await SendAsync<IssuedMagicLink>(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/invitations", owner.SessionToken,
            new { email = UniqueEmail("explicit-member") });
        var member = await RedeemAsync(invitation.Token);
        Assert.Null(member.Principal.SelectedWorkspaceId);

        var beforeSelection = await SendRawAsync(HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/EXPLICIT/create", member.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, beforeSelection.StatusCode);
    }

    async Task<RedeemedMagicLink> SignInAsync(string email)
    {
        var link = await SendAsync<IssuedMagicLink>(HttpMethod.Post, "/v1/auth/magic-links", null, new { email });
        return await RedeemAsync(link.Token);
    }

    Task<RedeemedMagicLink> RedeemAsync(string token) =>
        SendAsync<RedeemedMagicLink>(HttpMethod.Post, "/v1/auth/magic-links/redeem", null, new { token });

    Task<WorkspaceAccess> CreateWorkspaceAsync(string token, string name) =>
        SendAsync<WorkspaceAccess>(HttpMethod.Post, "/v1/workspaces", token, new { name });

    async Task SelectAsync(string token, string workspaceId)
    {
        var response = await SendRawAsync(HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token);
        response.EnsureSuccessStatusCode();
    }

    async Task<T> SendAsync<T>(HttpMethod method, string path, string? token, object? body = null)
    {
        var response = await SendRawAsync(method, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";
}
