using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class SongPackageJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Member_authors_previews_overrides_optional_lyrics_and_publishes_immutable_revision()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "package-member");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Package band" });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);
        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken,
            new { title = "Live Song", artist = "The Band" });
        var document = new SongPackageDocument(
            new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("hint-1", PackageHintType.LiveBand, null, null, "Play the opening riff")], null);

        var saved = await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            member.SessionToken, document);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var initial = await PostAsync<SongPackageReadiness>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/readiness",
            member.SessionToken, new { acceptedWarningCodes = Array.Empty<string>() });
        Assert.False(initial.CanPublish);
        Assert.Contains(initial.Findings, x => x.Code == "lyrics.missing" && x.CanOverride);
        Assert.Single(initial.Preview.Hints);

        var blockedPublish = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/publish",
            member.SessionToken, new { revisionNote = "Initial live package", acceptedWarningCodes = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedPublish.StatusCode);
        var published = await PostAsync<SongPackageRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/publish",
            member.SessionToken, new { revisionNote = "Initial live package", acceptedWarningCodes = new[] { "lyrics.missing" } });
        Assert.Equal(1, published.RevisionNumber);
        Assert.Equal(["lyrics.missing"], published.AcceptedWarningCodes);

        var changed = document with { Hints = [new("hint-1", PackageHintType.Text, "A changed draft", null, null)] };
        (await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            member.SessionToken, changed)).EnsureSuccessStatusCode();
        var revisions = await GetAsync<SongPackageRevision[]>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/revisions",
            member.SessionToken);
        Assert.Single(revisions);
        Assert.Equal(PackageHintType.LiveBand, revisions[0].Document.Hints[0].Type);
    }

    [Fact]
    public async Task Another_workspace_cannot_read_or_publish_the_package()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "package-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken,
            new { name = "Private package band" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);
        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", owner.SessionToken,
            new { title = "Secret", artist = "Band" });
        var document = new SongPackageDocument(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("hint", PackageHintType.Text, "Private clue", null, null)], null);
        (await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            owner.SessionToken, document)).EnsureSuccessStatusCode();

        var attacker = await SignInAsync(client, "package-attacker");
        var other = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", attacker.SessionToken,
            new { name = "Other band" });
        await SelectAsync(client, attacker.SessionToken, other.WorkspaceId);
        var hidden = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{other.WorkspaceId}/catalog/{entry.CatalogEntryId}/package", attacker.SessionToken);
        var missing = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{other.WorkspaceId}/catalog/entry_missing/package", attacker.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(missing.StatusCode, hidden.StatusCode);
    }

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null,
            new { token = link.Token });
    }
    static async Task SelectAsync(HttpClient client, string token, string workspaceId) =>
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token)).EnsureSuccessStatusCode();
    static async Task<T> PostAsync<T>(HttpClient client, string path, string? token, object? body)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, token, body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    static async Task<T> GetAsync<T>(HttpClient client, string path, string token)
    {
        var response = await SendAsync(client, HttpMethod.Get, path, token); response.EnsureSuccessStatusCode();
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
