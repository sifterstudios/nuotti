using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.SessionSnapshots;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class SessionSnapshotJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Performer_captures_once_and_only_paired_agent_reads_exact_snapshot()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "snapshot");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Snapshot band" });
        await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspace.WorkspaceId}/select", member.SessionToken);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SNAP1/create", member.SessionToken)).EnsureSuccessStatusCode();
        var entry = await PostAsync<PrivateCatalogEntry>(client, $"/v1/workspaces/{workspace.WorkspaceId}/catalog",
            member.SessionToken, new { title = "Song", artist = "Band" });
        var document = new SongPackageDocument(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("hint-1", PackageHintType.Text, "A clue", null, null)], null);
        (await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            member.SessionToken, document)).EnsureSuccessStatusCode();
        var package = await PostAsync<SongPackageRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/publish", member.SessionToken,
            new { acceptedWarningCodes = new[] { "lyrics.missing" } });
        var request = new CreateSessionSetlistSnapshotRequest([new(package.RevisionId)],
            new("standard", 1), ["song.1.lyrics-missing"]);
        var snapshot = await PostAsync<SessionSetlistSnapshot>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SNAP1/setlist-snapshot", member.SessionToken, request);
        Assert.Equal(package.RevisionId, Assert.Single(snapshot.Songs).PackageRevisionId);
        Assert.Equal("standard", snapshot.ScoringPolicy.PolicyId);

        var duplicate = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SNAP1/setlist-snapshot", member.SessionToken, request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/SNAP1/show-agent/pairings", member.SessionToken, null);
        var agent = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Venue laptop" });
        var agentSnapshot = await GetAsync<SessionSetlistSnapshot>(client, "/v1/show-agent/setlist-snapshot", agent.AccessToken);
        Assert.Equal(snapshot.SnapshotId, agentSnapshot.SnapshotId);
    }

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
        return await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = link.Token });
    }
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
