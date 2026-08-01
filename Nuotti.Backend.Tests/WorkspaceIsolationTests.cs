using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Workspaces;
using Xunit;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Two-Workspace adversarial isolation suite — any leakage fails the release gate (#263).
/// </summary>
public sealed class WorkspaceIsolationTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    [Trait("Category", "Isolation")]
    public async Task Two_workspaces_cannot_read_each_others_sessions_assets_or_diagnostics()
    {
        using var client = _factory.CreateClient();

        var ownerA = await SignInAsync(client, "iso-a");
        var wsA = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", ownerA.SessionToken, new { name = "Workspace A" });
        await SelectAsync(client, ownerA.SessionToken, wsA.WorkspaceId);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsA.WorkspaceId}/sessions/ISOA1/create", ownerA.SessionToken)).EnsureSuccessStatusCode();

        var ownerB = await SignInAsync(client, "iso-b");
        var wsB = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", ownerB.SessionToken, new { name = "Workspace B" });
        await SelectAsync(client, ownerB.SessionToken, wsB.WorkspaceId);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsB.WorkspaceId}/sessions/ISOB1/create", ownerB.SessionToken)).EnsureSuccessStatusCode();

        // B must not see A's session create surface / pairings / diagnostics / catalog using B's token on A's ids.
        var foreignSession = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsA.WorkspaceId}/sessions/ISOA1/start", ownerB.SessionToken);
        Assert.True(IsDenied(foreignSession.StatusCode), $"session start leaked: {foreignSession.StatusCode}");

        var foreignPairing = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsA.WorkspaceId}/sessions/ISOA1/show-agent/pairings", ownerB.SessionToken);
        Assert.True(IsDenied(foreignPairing.StatusCode), $"pairing leaked: {foreignPairing.StatusCode}");

        var foreignExport = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsA.WorkspaceId}/diagnostics/export", ownerB.SessionToken);
        Assert.True(IsDenied(foreignExport.StatusCode), $"diagnostics leaked: {foreignExport.StatusCode}");

        var foreignCatalog = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{wsA.WorkspaceId}/catalog", ownerB.SessionToken,
            new { title = "LeakProbe", artist = "Attacker" });
        Assert.True(IsDenied(foreignCatalog.StatusCode), $"catalog leaked: {foreignCatalog.StatusCode}");

        var foreignSnapshot = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{wsA.WorkspaceId}/sessions/ISOA1/setlist-snapshot", ownerB.SessionToken);
        Assert.True(IsDenied(foreignSnapshot.StatusCode), $"snapshot leaked: {foreignSnapshot.StatusCode}");

        var foreignMembers = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{wsA.WorkspaceId}/members", ownerB.SessionToken);
        Assert.True(IsDenied(foreignMembers.StatusCode), $"members leaked: {foreignMembers.StatusCode}");

        // Bodies must not echo the foreign workspace id when denied.
        foreach (var response in new[] { foreignSession, foreignPairing, foreignExport, foreignCatalog, foreignSnapshot, foreignMembers })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(wsA.WorkspaceId, body, StringComparison.Ordinal);
        }
    }

    static bool IsDenied(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound;

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = link.Token });
    }

    static async Task SelectAsync(HttpClient client, string token, string workspaceId) =>
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token)).EnsureSuccessStatusCode();

    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
