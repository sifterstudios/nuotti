using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nuotti.Performer.Services;

/// <summary>
/// Issues short pairing codes and observes/revokes venue devices (Projector, Show Agent) for a session.
/// </summary>
public sealed class ShowAgentPairingClient(HttpClient http)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ShowAgentPairingCodeDto> IssuePairingCodeAsync(
        string workspaceId, string sessionCode, string token, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post, Path(workspaceId, sessionCode, suffix: "/pairings"), token);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShowAgentPairingCodeDto>(Json, ct))!;
    }

    public async Task<IReadOnlyList<ShowAgentStatusDto>> ListStatusesAsync(
        string workspaceId, string sessionCode, string token, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get, Path(workspaceId, sessionCode), token);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<ShowAgentStatusDto[]>(Json, ct)) ?? [];
    }

    public async Task<bool> RevokeAsync(
        string workspaceId, string sessionCode, string token, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Delete, Path(workspaceId, sessionCode), token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        await EnsureAsync(response, ct);
        return true;
    }

    static string Path(string workspaceId, string sessionCode, string suffix = "") =>
        $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/sessions/{Uri.EscapeDataString(sessionCode)}/show-agent{suffix}";

    static HttpRequestMessage Request(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        return request;
    }

    static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var payload = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var problem = JsonSerializer.Deserialize<AuthoringProblem>(payload, Json);
            throw new InvalidOperationException(
                problem?.Detail ?? problem?.Title ?? $"Backend returned {(int)response.StatusCode}.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}.");
        }
    }
}

public sealed record ShowAgentPairingCodeDto(string Code, DateTimeOffset ExpiresAt);

[JsonConverter(typeof(JsonStringEnumConverter<ShowAgentDeviceState>))]
public enum ShowAgentDeviceState { Offline, Ready, Playing, Error }

public sealed record ShowAgentStatusDto(
    string AgentId,
    string Name,
    string WorkspaceId,
    string SessionCode,
    ShowAgentDeviceState State,
    string? Detail,
    DateTimeOffset? LastSeenAt,
    bool Revoked);
