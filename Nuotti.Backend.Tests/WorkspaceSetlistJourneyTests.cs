using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.Setlists;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class WorkspaceSetlistJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Member_picks_published_library_songs_into_ordered_setlist()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "setlist-member");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Setlist band" });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);

        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken,
            new { title = "Opener", artist = "The Band" });
        var document = new SongPackageDocument(
            new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("hint-1", PackageHintType.LiveBand, null, null, "Play the opening riff")], null);
        (await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            member.SessionToken, document)).EnsureSuccessStatusCode();
        var published = await PostAsync<SongPackageRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/publish",
            member.SessionToken, new { acceptedWarningCodes = new[] { "lyrics.missing" } });

        var available = await GetAsync<PublishedLibrarySong[]>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/library/published", member.SessionToken);
        Assert.Contains(available, x => x.PackageRevisionId == published.RevisionId);

        var saved = await PutAsync<WorkspaceSetlist>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/setlist", member.SessionToken,
            new { songs = new[] { new { packageRevisionId = published.RevisionId } } });
        Assert.Single(saved.Songs);
        Assert.Equal(published.RevisionId, saved.Songs[0].PackageRevisionId);

        var loaded = await GetAsync<WorkspaceSetlist>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/setlist", member.SessionToken);
        Assert.Equal(published.RevisionId, loaded.Songs[0].PackageRevisionId);

        var bad = await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/setlist", member.SessionToken,
            new { songs = new[] { new { packageRevisionId = "pkg_missing" } } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
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
