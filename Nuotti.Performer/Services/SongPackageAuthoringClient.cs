using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nuotti.Performer.Services;

public sealed class SongPackageAuthoringClient(HttpClient http)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CatalogEntryDto>> ListCatalogAsync(string workspaceId, string token,
        CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog", token);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<CatalogEntryDto[]>(Json, ct)) ?? [];
    }

    public async Task<CatalogEntryDto> CreateCatalogEntryAsync(string workspaceId, string token,
        string title, string artist, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog", token,
            new { title, artist });
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<CatalogEntryDto>(Json, ct))!;
    }

    public async Task<CatalogEntryDto> UpdateCatalogEntryAsync(string workspaceId, string catalogEntryId, string token,
        string title, string artist, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Put,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog/{Uri.EscapeDataString(catalogEntryId)}",
            token, new { title, artist });
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<CatalogEntryDto>(Json, ct))!;
    }

    public async Task<AuthoringDocument?> GetAsync(string workspaceId, string catalogEntryId, string token,
        CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog/{Uri.EscapeDataString(catalogEntryId)}/package",
            token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<AuthoringDraft>(Json, ct))?.Document;
    }

    public async Task SaveAsync(string workspaceId, string catalogEntryId, string token,
        AuthoringDocument document, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Put,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog/{Uri.EscapeDataString(catalogEntryId)}/package",
            token, document);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
    }

    public async Task<AuthoringReadiness> EvaluateAsync(string workspaceId, string catalogEntryId, string token,
        IReadOnlyCollection<string> acceptedWarnings, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog/{Uri.EscapeDataString(catalogEntryId)}/package/readiness",
            token, new { acceptedWarningCodes = acceptedWarnings });
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<AuthoringReadiness>(Json, ct))!;
    }

    public async Task<PublishedPackage> PublishAsync(string workspaceId, string catalogEntryId, string token,
        IReadOnlyCollection<string> acceptedWarnings, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Post,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/catalog/{Uri.EscapeDataString(catalogEntryId)}/package/publish",
            token, new { acceptedWarningCodes = acceptedWarnings });
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PublishedPackage>(Json, ct))!;
    }

    public async Task<IReadOnlyList<PublishedLibrarySongDto>> ListPublishedLibraryAsync(string workspaceId, string token,
        CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/library/published", token);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<PublishedLibrarySongDto[]>(Json, ct)) ?? [];
    }

    public async Task<WorkspaceSetlistDto> GetSetlistAsync(string workspaceId, string token,
        CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Get,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/setlist", token);
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<WorkspaceSetlistDto>(Json, ct))!;
    }

    public async Task<WorkspaceSetlistDto> SaveSetlistAsync(string workspaceId, string token,
        IReadOnlyList<SetlistSongDto> songs, CancellationToken ct = default)
    {
        using var request = Request(HttpMethod.Put,
            $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/setlist", token,
            new { songs });
        using var response = await http.SendAsync(request, ct);
        await EnsureAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<WorkspaceSetlistDto>(Json, ct))!;
    }

    static HttpRequestMessage Request(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
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
            throw new InvalidOperationException(problem?.Detail ?? problem?.Title ?? $"Backend returned {(int)response.StatusCode}.");
        }
        catch (JsonException) { throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}."); }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<AuthoringPlaybackMode>))]
public enum AuthoringPlaybackMode { LiveOnly, ClickOnly, BackingOnly, BackingWithClick }
[JsonConverter(typeof(JsonStringEnumConverter<AuthoringHintType>))]
public enum AuthoringHintType { Text, Image, Visual, LiveBand }
[JsonConverter(typeof(JsonStringEnumConverter<AuthoringSeverity>))]
public enum AuthoringSeverity { Ready, Warning, Blocking }

public sealed record AuthoringPlayback(AuthoringPlaybackMode Mode, string? BackingAssetRevisionId,
    string? ClickAssetRevisionId, long SongStartOffsetMs, long? MasterDurationMs, long? BackingDurationMs,
    long? ClickDurationMs, IReadOnlyList<int> BackingOutputChannels, IReadOnlyList<int> ClickOutputChannels);
public sealed record AuthoringHint(string HintId, AuthoringHintType Type, string? Text,
    string? AssetRevisionId, string? PerformerCue);
public sealed record AuthoringLyrics(string Lrc, long OffsetMs);
public sealed record AuthoringDocument(AuthoringPlayback Playback, IReadOnlyList<AuthoringHint> Hints,
    AuthoringLyrics? Lyrics);
public sealed record AuthoringDraft(string WorkspaceId, string CatalogEntryId, AuthoringDocument Document,
    string UpdatedBy, DateTimeOffset UpdatedAt);
public sealed record AuthoringFinding(string Code, AuthoringSeverity Severity, string Section, string Title,
    string Consequence, string RecommendedAction, bool CanOverride);
public sealed record AuthoringHintPreview(int Order, AuthoringHintType Type, string DisplayText, string? AssetRevisionId);
public sealed record AuthoringLyricLine(long ActivationMs, string Text);
public sealed record AuthoringPreview(IReadOnlyList<AuthoringHintPreview> Hints,
    IReadOnlyList<AuthoringLyricLine> Lyrics, long? MasterDurationMs);
public sealed record AuthoringReadiness(bool CanPublish, IReadOnlyList<AuthoringFinding> Findings,
    AuthoringPreview Preview);
public sealed record PublishedPackage(string RevisionId, int RevisionNumber, DateTimeOffset PublishedAt);
public sealed record AuthoringProblem(string Title, string Detail);
public sealed record PublishedLibrarySongDto(string CatalogEntryId, string Title, string Artist,
    string PackageRevisionId, int RevisionNumber, DateTimeOffset PublishedAt);
public sealed record SetlistSongDto(string PackageRevisionId, string? LyricTrackRevisionId = null);
public sealed record WorkspaceSetlistDto(string WorkspaceId, IReadOnlyList<SetlistSongDto> Songs,
    DateTimeOffset UpdatedAt, string UpdatedBy);
