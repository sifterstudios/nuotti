using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nuotti.AudioEngine;

public sealed record CloudAgentCommand(long Sequence, string MessageType, JsonElement Payload);
public sealed record CloudAgentLease(string AccessToken, DateTimeOffset ExpiresAt, string WorkspaceId, string SessionCode);
public sealed record CloudSnapshotAsset(string RevisionId, string AssetType, string Sha256, long Size, bool Required);
public sealed record CloudSessionSnapshot(string SnapshotId, string WorkspaceId, string SessionCode, int Version,
    IReadOnlyList<CloudSnapshotAsset> Assets);
public sealed record CloudAssetGrant(Uri DownloadUri, DateTimeOffset ExpiresAt);
sealed record PairResponse(string AgentId, string Credential, string AccessToken, DateTimeOffset AccessTokenExpiresAt);

public sealed class ShowAgentCloudClient(HttpClient http, IShowAgentCredentialStore credentials)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    CloudAgentLease? _lease;

    public async Task PairAsync(string code, string name, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/v1/show-agent/pair", new { code, name }, Json, ct);
        response.EnsureSuccessStatusCode();
        var paired = (await response.Content.ReadFromJsonAsync<PairResponse>(Json, ct))!;
        credentials.Save(paired.Credential);
        _lease = new(paired.AccessToken, paired.AccessTokenExpiresAt, "", "");
    }

    public async Task<CloudAgentLease?> EnsureLeaseAsync(CancellationToken ct = default)
    {
        if (_lease is not null && _lease.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30)
            && _lease.WorkspaceId.Length > 0) return _lease;
        var credential = credentials.Load();
        if (string.IsNullOrWhiteSpace(credential)) return null;
        using var response = await http.PostAsJsonAsync("/v1/show-agent/token", new { credential }, Json, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            credentials.Delete();
            _lease = null;
            return null;
        }
        response.EnsureSuccessStatusCode();
        _lease = (await response.Content.ReadFromJsonAsync<CloudAgentLease>(Json, ct))!;
        return _lease;
    }

    public async Task<IReadOnlyList<CloudAgentCommand>?> PollAsync(long after, CancellationToken ct = default)
    {
        var lease = await EnsureLeaseAsync(ct);
        if (lease is null) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/show-agent/commands?after={after}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lease.AccessToken);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) { _lease = null; return []; }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CloudAgentCommand[]>(Json, ct) ?? [];
    }

    public async Task<CloudSessionSnapshot?> GetSnapshotAsync(CancellationToken ct = default)
    {
        var lease = await EnsureLeaseAsync(ct);
        if (lease is null) return null;
        using var request = AgentRequest(HttpMethod.Get, "/v1/show-agent/setlist-snapshot", lease.AccessToken);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized) { _lease = null; return null; }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CloudSessionSnapshot>(Json, ct);
    }

    public async Task<CloudAssetGrant> GetAssetGrantAsync(string revisionId, CancellationToken ct = default)
    {
        var lease = await EnsureLeaseAsync(ct) ?? throw new InvalidOperationException("Show Agent lease is unavailable.");
        using var request = AgentRequest(HttpMethod.Post,
            $"/v1/show-agent/assets/{Uri.EscapeDataString(revisionId)}/download", lease.AccessToken);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CloudAssetGrant>(Json, ct))!;
    }

    public async Task<bool> ReportStatusAsync(string state, string? detail, CancellationToken ct = default)
    {
        var lease = await EnsureLeaseAsync(ct);
        if (lease is null) return false;
        using var request = new HttpRequestMessage(HttpMethod.Put, "/v1/show-agent/status")
        {
            Content = JsonContent.Create(new { state, detail }, options: Json)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lease.AccessToken);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) { _lease = null; return false; }
        response.EnsureSuccessStatusCode();
        return true;
    }

    public long LoadCursor() => _lease is null ? 0 : credentials.LoadCursor(_lease.WorkspaceId, _lease.SessionCode);

    public void CommitCursor(long sequence)
    {
        if (_lease is null) throw new InvalidOperationException("Show Agent lease is not established.");
        credentials.SaveCursor(_lease.WorkspaceId, _lease.SessionCode, sequence);
    }

    public static T? DeserializePayload<T>(JsonElement payload) => payload.Deserialize<T>(Json);

    static HttpRequestMessage AgentRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
