using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nuotti.Backend;
using Nuotti.Backend.Assets;
using Nuotti.Backend.ShowAgents;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Governance;

namespace Nuotti.Backend.Tests;

public sealed class GovernanceJourneyTests(WebApplicationFactory<QuizHub> baseFactory)
    : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = baseFactory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Verbose_export_requires_entitlement_and_takedown_blocks_download()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "gov-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Gov band" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);

        var deniedExport = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/diagnostics/export", owner.SessionToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedExport.StatusCode);

        var elevated = await PostAsync<JsonElement>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/diagnostics/verbose", owner.SessionToken,
            new { minutes = 10, verbose = true });
        Assert.Equal("Verbose", elevated.GetProperty("level").GetString());

        var export = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/diagnostics/export", owner.SessionToken);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/zip", export.Content.Headers.ContentType?.MediaType);
        Assert.True((await export.Content.ReadAsByteArrayAsync()).Length > 20);

        var entry = await PostAsync<PrivateCatalogEntry>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog", owner.SessionToken,
            new { title = "Restricted", artist = "Band" });
        var bytes = "gov-audio"u8.ToArray();
        var upload = await PostAsync<PrivateAssetUploadGrant>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/catalog/{entry.CatalogEntryId}/asset-uploads", owner.SessionToken,
            new
            {
                assetType = "backing-track", contentType = "audio/wav", size = bytes.LongLength,
                provenance = new
                {
                    source = "owned", rightsBasis = "original", territory = "NO",
                    permittedUses = new[] { "backing-track" }, rightsExpiresAt = (DateTimeOffset?)null,
                    supportingDocumentReference = "gov-case"
                }
            });
        var metadata = _factory.Services.GetRequiredService<IPrivateAssetMetadataStore>();
        var objects = Assert.IsType<InMemoryPrivateAssetObjectStore>(_factory.Services.GetRequiredService<IPrivateAssetObjectStore>());
        var objectKey = await metadata.GetObjectKeyAsync(workspace.WorkspaceId, upload.Revision.RevisionId);
        objects.PutDirect(objectKey!, bytes, "audio/wav");
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var published = await PostAsync<PrivateAssetRevision>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{upload.Revision.RevisionId}/publish", owner.SessionToken,
            new { sha256 });

        await PostAsync<TakedownCase>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/assets/{published.RevisionId}/takedown", owner.SessionToken,
            new { reason = "rights dispute" });
        var blocked = await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/revisions/{published.RevisionId}/download", owner.SessionToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Show_agent_token_includes_verifiable_signed_lease()
    {
        using var client = _factory.CreateClient();
        var owner = await SignInAsync(client, "lease-owner");
        var workspace = await PostAsync<WorkspaceAccess>(client, "/v1/workspaces", owner.SessionToken, new { name = "Lease band" });
        await SelectAsync(client, owner.SessionToken, workspace.WorkspaceId);
        (await SendAsync(client, HttpMethod.Post,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/LEASE1/create", owner.SessionToken)).EnsureSuccessStatusCode();

        var pairing = await PostAsync<ShowAgentPairingCode>(client,
            $"/v1/workspaces/{workspace.WorkspaceId}/sessions/LEASE1/show-agent/pairings", owner.SessionToken, null);
        var paired = await PostAsync<PairedShowAgent>(client, "/v1/show-agent/pair", null,
            new { code = pairing.Code, name = "Lease laptop" });
        var tokenResponse = await PostAsync<JsonElement>(client, "/v1/show-agent/token", null,
            new { credential = paired.Credential });
        var signed = tokenResponse.GetProperty("signedLease");
        Assert.False(string.IsNullOrWhiteSpace(signed.GetProperty("signature").GetString()));

        var lease = JsonSerializer.Deserialize<SignedLease>(signed.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
        var governance = _factory.Services.GetRequiredService<Nuotti.Backend.Governance.ProductionGovernance>();
        Assert.True(governance.LeaseIssuer.TryVerify(lease, DateTimeOffset.UtcNow, out _));
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
