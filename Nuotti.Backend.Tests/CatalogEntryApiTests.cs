using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class CatalogEntryApiTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Member_lists_gets_and_updates_catalog_entries()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "catalog-api");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Catalog band" });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);

        var created = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken,
            new { title = "First Song", artist = "The Band" });
        var listed = await GetAsync<PrivateCatalogEntry[]>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken);
        Assert.Contains(listed, e => e.CatalogEntryId == created.CatalogEntryId);

        var fetched = await GetAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{created.CatalogEntryId}", member.SessionToken);
        Assert.Equal("First Song", fetched.Title);

        var updated = await PutAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{created.CatalogEntryId}", member.SessionToken,
            new { title = "Renamed Song", artist = "New Artist" });
        Assert.Equal("Renamed Song", updated.Title);
        Assert.Equal("New Artist", updated.Artist);
    }

    [Fact]
    public async Task Another_workspace_cannot_list_or_update_entries()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "catalog-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken,
            new { name = "Private catalog" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);
        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", owner.SessionToken,
            new { title = "Secret", artist = "Band" });

        var attacker = await SignInAsync(client, "catalog-attacker");
        var other = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", attacker.SessionToken,
            new { name = "Other" });
        await SelectAsync(client, attacker.SessionToken, other.WorkspaceId);

        var list = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", attacker.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        var update = await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}", attacker.SessionToken,
            new { title = "Hijacked", artist = "Nope" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = link.Token });
    }

    static async Task SelectAsync(HttpClient client, string token, string workspaceId) =>
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token)).EnsureSuccessStatusCode();

    static async Task<T> GetAsync<T>(HttpClient client, string path, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, path, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<T> PutAsync<T>(HttpClient client, string path, string token, object body)
    {
        var response = await SendAsync(client, HttpMethod.Put, path, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
