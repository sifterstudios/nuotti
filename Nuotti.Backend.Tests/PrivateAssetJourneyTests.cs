using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.Workspaces;

namespace Nuotti.Backend.Tests;

public sealed class PrivateAssetJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Concurrent_publication_claims_allow_only_one_finalizer()
    {
        var store = new InMemoryPrivateAssetMetadataStore();
        var entry = await store.CreateEntryAsync("workspace", "Song", "Artist", "owner");
        var provenance = new AssetProvenance("owned", "original recording", "NO",
            ["backing-track"], null, "rights-case");
        var draft = await store.CreateDraftAsync("workspace", entry.CatalogEntryId, "backing-track",
            "audio/wav", 4, provenance, "owner");

        var claims = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => store.TryBeginFinalizationAsync("workspace", draft!.Value.Revision.RevisionId)));

        Assert.Single(claims, claim => claim is not null);
    }

    [Fact]
    public async Task Stale_publication_claim_can_be_recovered()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-31T12:00:00Z"));
        var store = new InMemoryPrivateAssetMetadataStore(time);
        var entry = await store.CreateEntryAsync("workspace", "Song", "Artist", "owner");
        var draft = await store.CreateDraftAsync("workspace", entry.CatalogEntryId, "backing-track", "audio/wav", 4,
            new("owned", "original", "NO", ["backing-track"], null, "rights-case"), "owner");
        Assert.NotNull(await store.TryBeginFinalizationAsync("workspace", draft!.Value.Revision.RevisionId));
        Assert.Null(await store.TryBeginFinalizationAsync("workspace", draft.Value.Revision.RevisionId));

        time.Advance(TimeSpan.FromMinutes(11));

        Assert.NotNull(await store.TryBeginFinalizationAsync("workspace", draft.Value.Revision.RevisionId));
    }

    [Fact]
    public async Task Member_uploads_directly_publishes_immutable_revision_and_owner_archives_it()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "asset-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Asset band" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);
        var invitation = await PostAsync<IssuedMagicLink>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/invitations", owner.SessionToken,
            new { email = $"asset-member-{Guid.NewGuid():N}@example.test" });
        var member = await PostAsync<RedeemedMagicLink>(client, "/v1/auth/magic-links/redeem", null, new { token = invitation.Token });
        await SelectAsync(client, member.SessionToken, workspace.WorkspaceId);

        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", member.SessionToken,
            new { title = "Midnight Train", artist = "The Examples" });
        var expiredUpload = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", member.SessionToken,
            new
            {
                assetType = "backing-track", contentType = "audio/wav", size = 10,
                provenance = new { source = "licensed", rightsBasis = "venue licence", territory = "NO", permittedUses = new[] { "backing-track" }, rightsExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), supportingDocumentReference = "expired-case" }
            });
        Assert.Equal(HttpStatusCode.BadRequest, expiredUpload.StatusCode);
        var arbitraryUse = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", member.SessionToken,
            new
            {
                assetType = "backing-track", contentType = "audio/wav", size = 10,
                provenance = new { source = "licensed", rightsBasis = "venue licence", territory = "NO", permittedUses = new[] { "anything-goes" }, rightsExpiresAt = (DateTimeOffset?)null, supportingDocumentReference = "bad-use" }
            });
        Assert.Equal(HttpStatusCode.BadRequest, arbitraryUse.StatusCode);
        var provenance = new AssetProvenance("licensed master", "venue playback licence", "NO",
            ["backing-track", "live-performance"], DateTimeOffset.UtcNow.AddYears(1), "rights-case-42");
        var bytes = "private-audio-bytes"u8.ToArray();
        var upload = await PostAsync<PrivateAssetUploadGrant>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", member.SessionToken,
            new { assetType = "backing-track", contentType = "audio/wav", size = bytes.LongLength, provenance });
        Assert.Equal("objects.invalid", upload.UploadUri.Host);
        Assert.DoesNotContain(workspace.WorkspaceId, upload.UploadUri.AbsoluteUri, StringComparison.Ordinal);

        var metadata = _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>();
        var objects = Assert.IsType<InMemoryPrivateAssetObjectStore>(_factory.Services.GetRequiredService<IPrivateAssetObjectStore>());
        var objectKey = await metadata.GetObjectKeyAsync(workspace.WorkspaceId, upload.Revision.RevisionId);
        objects.PutDirect(objectKey!, bytes, "audio/wav"); // Models the client PUT to object storage; no Backend byte endpoint exists.
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var published = await PostAsync<PrivateAssetRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{upload.Revision.RevisionId}/publish", member.SessionToken,
            new { sha256 });
        Assert.Equal(AssetRevisionStatus.Published, published.Status);
        Assert.Equal(sha256, published.Sha256);
        Assert.Equal(provenance.Source, published.Provenance.Source);
        Assert.Equal(provenance.RightsBasis, published.Provenance.RightsBasis);
        Assert.Equal(provenance.Territory, published.Provenance.Territory);
        Assert.Equal(provenance.PermittedUses, published.Provenance.PermittedUses);
        Assert.Equal(provenance.RightsExpiresAt, published.Provenance.RightsExpiresAt);
        Assert.Equal(provenance.SupportingDocumentReference, published.Provenance.SupportingDocumentReference);
        var sealedKey = await metadata.GetObjectKeyAsync(workspace.WorkspaceId, published.RevisionId);
        Assert.NotEqual(objectKey, sealedKey);
        objects.PutDirect(objectKey!, "mutated-after-publish"u8.ToArray(), "audio/wav");
        Assert.Equal(bytes, objects.GetDirect(sealedKey!));

        var republish = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{published.RevisionId}/publish", member.SessionToken,
            new { sha256 });
        Assert.Equal(HttpStatusCode.Conflict, republish.StatusCode);
        var download = await PostAsync<PrivateAssetDownloadGrant>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{published.RevisionId}/download", member.SessionToken, null);
        Assert.Equal("objects.invalid", download.DownloadUri.Host);

        var archived = await DeleteAsync<PrivateAssetRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{published.RevisionId}", owner.SessionToken);
        Assert.Equal(AssetRevisionStatus.Archived, archived.Status);
        var afterArchive = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{published.RevisionId}/download", member.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, afterArchive.StatusCode);
    }

    [Fact]
    public async Task Another_workspace_cannot_discover_revision_object_or_grants()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "isolation-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Private band" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);
        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", owner.SessionToken, new { title = "Secret", artist = "Band" });
        var upload = await PostAsync<PrivateAssetUploadGrant>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", owner.SessionToken,
            new
            {
                assetType = "image", contentType = "image/png", size = 4,
                provenance = new { source = "commissioned", rightsBasis = "owned", territory = "NO", permittedUses = new[] { "visual-hint" }, rightsExpiresAt = (DateTimeOffset?)null, supportingDocumentReference = "case-7" }
            });

        var attacker = await SignInAsync(client, "asset-attacker");
        var other = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", attacker.SessionToken, new { name = "Other band" });
        await SelectAsync(client, attacker.SessionToken, other.WorkspaceId);
        var hidden = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{other.WorkspaceId}/revisions/{upload.Revision.RevisionId}", attacker.SessionToken);
        var random = await SendAsync(client, HttpMethod.Get,
            $"/v1/workspaces/{other.WorkspaceId}/revisions/rev_missing", attacker.SessionToken);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(random.StatusCode, hidden.StatusCode);
        Assert.Null(await _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>()
            .GetObjectKeyAsync(other.WorkspaceId, upload.Revision.RevisionId));
    }

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
        var response = await SendAsync(client, HttpMethod.Post, path, token, body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    static async Task<T> DeleteAsync<T>(HttpClient client, string path, string token)
    {
        var response = await SendAsync(client, HttpMethod.Delete, path, token); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
