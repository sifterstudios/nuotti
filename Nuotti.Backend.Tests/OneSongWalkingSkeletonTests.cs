using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Sessions;
using Nuotti.Backend.SessionSnapshots;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Issue #255 — one-song cloud-to-venue walking skeleton at the HTTP + Show Agent seam:
/// Participant joins, Hint + Window, answer, Reveal, Prepare, Start with cache-resolved PlayTrack,
/// and one lyric line from the captured Session Setlist Snapshot.
/// </summary>
public sealed class OneSongWalkingSkeletonTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Performer_drives_hint_window_answer_reveal_prepare_and_start_to_show_agent()
    {
        using var client = _factory.CreateClient();
        var member = await SignInAsync(client, "walk");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", member.SessionToken,
            new { name = "Walking skeleton band" });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);

        const string session = "WALK1";
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{session}/create", member.SessionToken))
            .EnsureSuccessStatusCode();
        Assert.Equal(workspace.WorkspaceId,
            _factory.Services.GetRequiredService<ISessionWorkspaceBinder>().Resolve(session));

        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken,
            new { title = "Walking Song", artist = "The Band" });
        var bytes = "walking-skeleton-audio"u8.ToArray();
        var upload = await PostAsync<PrivateAssetUploadGrant>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", member.SessionToken,
            new
            {
                assetType = "backing-track", contentType = "audio/wav", size = bytes.LongLength,
                provenance = new
                {
                    source = "owned", rightsBasis = "original recording", territory = "NO",
                    permittedUses = new[] { "backing-track" }, rightsExpiresAt = (DateTimeOffset?)null,
                    supportingDocumentReference = "walk-case"
                }
            });
        var objects = Assert.IsType<InMemoryPrivateAssetObjectStore>(
            _factory.Services.GetRequiredService<IPrivateAssetObjectStore>());
        var metadata = _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>();
        var objectKey = await metadata.GetObjectKeyAsync(workspace.WorkspaceId, upload.Revision.RevisionId);
        objects.PutDirect(objectKey!, bytes, "audio/wav");
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var publishedAsset = await PostAsync<PrivateAssetRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{upload.Revision.RevisionId}/publish",
            member.SessionToken, new { sha256 });

        const string lrc = "[00:00.00]First line of the reveal\n[00:05.00]Second line";
        var document = new SongPackageDocument(
            new(PlaybackMode.BackingOnly, publishedAsset.RevisionId, null, 2_000, 10_000, 8_000, null, [1, 2], []),
            [new("hint-1", PackageHintType.Text, "A walking clue", null, null)],
            new(lrc, 0));
        (await SendAsync(client, HttpMethod.Put,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package",
            member.SessionToken, document)).EnsureSuccessStatusCode();
        var publishResponse = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/package/publish",
            member.SessionToken, new { revisionNote = "Walk", acceptedWarningCodes = Array.Empty<string>() });
        if (!publishResponse.IsSuccessStatusCode)
        {
            var body = await publishResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Package publish failed: {(int)publishResponse.StatusCode} {body}");
        }
        var package = (await publishResponse.Content.ReadFromJsonAsync<SongPackageRevision>())!;

        var snapshot = await PostAsync<SessionSetlistSnapshot>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{session}/setlist-snapshot", member.SessionToken,
            new CreateSessionSetlistSnapshotRequest([new(package.RevisionId)], new("standard", 1), []));
        Assert.Equal(publishedAsset.RevisionId, Assert.Single(snapshot.Assets).RevisionId);
        var capturedLyric = Assert.Single(snapshot.Songs).LyricTrack;
        Assert.NotNull(capturedLyric);
        Assert.Contains("First line of the reveal", capturedLyric!.Lrc);

        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/{session}/show-agent/pairings",
            member.SessionToken, null);
        var agent = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Venue laptop" });

        await PostPhaseAsync(client, "start-game", session, new StartGame
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });
        // Catalog comes from UpdateCatalog for RevealAnswer's SongRef; keep it aligned with the package.
        var manifest = new SetlistManifest
        {
            Songs = { new SetlistManifest.SongEntry { Title = "Walking Song", Artist = "The Band", File = "https://example.test/walking-song.mp3" } }
        };
        var manifestResponse = await SendAsync(client, HttpMethod.Post, $"/api/manifest/{session}", null, manifest);
        manifestResponse.EnsureSuccessStatusCode();

        await PostJsonAsync(client, $"/api/pushQuestion/{session}", new QuestionPushed("Who sang it?", ["A", "B", "C"])
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });
        await PostPhaseAsync(client, "open-answers", session, new OpenAnswers
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });

        await using var audienceHub = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/hub"), o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();
        await audienceHub.StartAsync();
        await audienceHub.InvokeAsync("Join", session, "audience", "walker-1", "device-walk-1");
        await audienceHub.InvokeAsync("SubmitAnswer", session, 0, Guid.Empty);

        var stateStore = _factory.Services.GetRequiredService<IGameStateStore>();
        if (!stateStore.TryGet(session, out var mid) || mid!.Answers.Count == 0)
        {
            var answer = new SubmitAnswer(null, 0)
            {
                SessionCode = session, IssuedByRole = Role.Audience, IssuedById = "walker-1"
            };
            var answered = await _factory.Services.GetRequiredService<ISessionCommandProcessor>()
                .ApplyAsync(session, Actor.Verified(Role.Audience, "walker-1"), answer,
                    workspaceId: workspace.WorkspaceId);
            Assert.Equal(Nuotti.Contracts.V1.Protocol.Outcome.Applied, answered.Outcome);
            Assert.True(answered.State is { Answers.Count: > 0 });
        }

        var songId = new SongId("song-1-walkingsong");
        await PostPhaseAsync(client, "give-hint", session, new GiveHint(new Hint(0, "A walking clue", null, songId))
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });
        await PostPhaseAsync(client, "lock-answers", session, new LockAnswers
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });
        var revealSong = new SongRef(songId, "Walking Song", "The Band");
        await PostPhaseAsync(client, "reveal-answer", session, new RevealAnswer(revealSong, 0)
        {
            SessionCode = session, IssuedByRole = Role.Performer, IssuedById = member.Principal.UserId
        });

        var prepareCorrelation = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        using (var prepareRequest = new HttpRequestMessage(HttpMethod.Post,
                   $"/v1/message/phase/prepare-playback/{session}"))
        {
            prepareRequest.Headers.Add("X-Correlation-Id", prepareCorrelation.ToString());
            prepareRequest.Content = JsonContent.Create(new PreparePlayback
            {
                CommandId = prepareCorrelation,
                SessionCode = session,
                IssuedByRole = Role.Performer,
                IssuedById = member.Principal.UserId
            });
            (await client.SendAsync(prepareRequest)).EnsureSuccessStatusCode();
        }

        var startCorrelation = Guid.Parse("11111111-2222-3333-4444-555555555555");
        using (var startRequest = new HttpRequestMessage(HttpMethod.Post,
                   $"/v1/message/phase/start-playback/{session}"))
        {
            startRequest.Headers.Add("X-Correlation-Id", startCorrelation.ToString());
            startRequest.Content = JsonContent.Create(new StartPlayback(revealSong.Id, publishedAsset.RevisionId)
            {
                CommandId = startCorrelation,
                SessionCode = session,
                IssuedByRole = Role.Performer,
                IssuedById = member.Principal.UserId
            });
            (await client.SendAsync(startRequest)).EnsureSuccessStatusCode();
        }

        var commands = await GetAsync<ShowAgentCommand[]>(client, "/v1/show-agent/commands?after=0", agent.AccessToken);
        Assert.Contains(commands, c => c.MessageType == "Prepare");
        var play = Assert.Single(commands, c => c.MessageType == "PlayTrack");
        var playPayload = JsonSerializer.SerializeToElement(play.Payload);
        Assert.Equal(publishedAsset.RevisionId,
            playPayload.TryGetProperty("assetRevisionId", out var assetProp)
                ? assetProp.GetString()
                : playPayload.GetProperty("AssetRevisionId").GetString());

        var state = _factory.Services.GetRequiredService<IGameStateStore>();
        Assert.True(state.TryGet(session, out var snapshotState));
        Assert.Equal(Phase.Play, snapshotState!.Phase);
        Assert.True(snapshotState.HintIndex >= 1);
        Assert.Single(snapshotState.Answers);
    }

    [Fact]
    public void Aspire_app_host_composes_every_walking_skeleton_role()
    {
        var host = File.ReadAllText(Path.Combine(RepoRoot(), "Nuotti", "Program.cs"));
        Assert.Contains("DistributedApplication.CreateBuilder", host);
        Assert.Contains("Nuotti_Backend", host);
        Assert.Contains("Nuotti_AudioEngine", host);
        Assert.Contains("Nuotti_Performer", host);
        Assert.Contains("Nuotti_Audience", host);
        Assert.Contains("Nuotti_Projector", host);
        Assert.Contains("show-agent", host);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Nuotti.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    static async Task PostPhaseAsync(HttpClient client, string route, string session, CommandBase cmd)
    {
        var response = await client.PostAsJsonAsync($"/v1/message/phase/{route}/{session}", cmd);
        response.EnsureSuccessStatusCode();
    }

    static async Task PostJsonAsync(HttpClient client, string path, object body)
    {
        (await SendAsync(client, HttpMethod.Post, path, null, body)).EnsureSuccessStatusCode();
    }

    static async Task SelectAsync(HttpClient client, string token, string workspaceId) =>
        (await SendAsync(client, HttpMethod.Post, $"/v1/workspaces/{workspaceId}/select", token)).EnsureSuccessStatusCode();

    static async Task<RedeemedMagicLink> SignInAsync(HttpClient client, string prefix)
    {
        var link = await PostAsync<IssuedMagicLink>(client, "/v1/auth/magic-links", null,
            new { email = $"{prefix}-{Guid.NewGuid():N}@example.test" });
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

    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
