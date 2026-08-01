using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nuotti.Performer.Services;

public sealed record WorkspaceFixtureDto(
    string WorkspaceId,
    string WorkspaceName,
    string Email,
    string SessionToken);

public sealed record CatalogEntryDto(
    string CatalogEntryId,
    string WorkspaceId,
    string Title,
    string Artist,
    string CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>A workspace the signed-in user belongs to, as returned by GET /v1/workspaces.</summary>
public sealed record WorkspaceAccessDto(string WorkspaceId, string Name, string Role);

public sealed record RedeemedMagicLinkDto(string SessionToken, WorkspacePrincipalDto Principal);

public sealed record WorkspacePrincipalDto(
    string UserId,
    string Email,
    string? SelectedWorkspaceId,
    string SessionId);

/// <summary>The outcome of redeeming a magic link, for the sign-in page to render.</summary>
public enum RedeemOutcome
{
    Succeeded,
    /// <summary>Expired, already used, or simply wrong. The Backend does not distinguish.</summary>
    Rejected,
    Failed
}

/// <summary>
/// Holds the active Workspace + member session for Song Library / Package authoring.
/// </summary>
/// <remarks>
/// Three ways in, in priority order: a token persisted from a previous visit, the Development
/// fixture, and static Dev:* configuration. Signing in through a magic link goes through
/// <see cref="RedeemAsync"/>, which persists the token so the first path wins next time.
/// </remarks>
public sealed class WorkspaceSession(
    IHttpClientFactory httpFactory,
    IConfiguration config,
    IHostEnvironment env,
    IWorkspaceSessionStore tokenStore)
{
    public const string HttpClientName = "nuotti-backend";

    readonly SemaphoreSlim _gate = new(1, 1);
    bool _initialized;

    public string? WorkspaceId { get; private set; }
    public string? WorkspaceName { get; private set; }
    public string? SessionToken { get; private set; }
    public string? CatalogEntryId { get; set; }
    public string? Error { get; private set; }
    public string? Email { get; private set; }

    /// <summary>A redeemed session exists. Says nothing about a workspace being selected.</summary>
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(SessionToken);

    /// <summary>Authenticated AND scoped to a workspace. Everything authoring-related needs this.</summary>
    public bool IsReady => IsAuthenticated && !string.IsNullOrWhiteSpace(WorkspaceId);

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        if (_initialized && IsReady) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized && IsReady) return;
            Error = null;

            // A token kept from a previous visit outranks any fixture: it is a real user's
            // session, and silently replacing it would hide who is actually signed in.
            var stored = await tokenStore.GetTokenAsync(ct);
            if (!string.IsNullOrWhiteSpace(stored) && await AdoptTokenAsync(stored, ct))
            {
                _initialized = true;
                return;
            }

            if (env.IsDevelopment())
            {
                try
                {
                    var fixture = await httpFactory.CreateClient(HttpClientName)
                        .GetFromJsonAsync<WorkspaceFixtureDto>("/v1/dev/fixture", ct);
                    if (fixture is not null)
                    {
                        WorkspaceId = fixture.WorkspaceId;
                        WorkspaceName = fixture.WorkspaceName;
                        SessionToken = fixture.SessionToken;
                        Email = fixture.Email;
                        _initialized = true;
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    Error = $"Could not load Development fixture: {ex.Message}";
                }
            }

            WorkspaceId = config["Dev:WorkspaceId"];
            SessionToken = config["Dev:SessionToken"];
            WorkspaceName = config["Dev:WorkspaceName"] ?? WorkspaceId;
            _initialized = true;
            if (!IsReady) Error ??= "Not signed in.";
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Asks the Backend to email a sign-in link. True when it accepted the request.</summary>
    public async Task<bool> RequestSignInAsync(string email, CancellationToken ct = default)
    {
        Error = null;
        try
        {
            using var response = await httpFactory.CreateClient(HttpClientName)
                .PostAsJsonAsync("/v1/auth/magic-links", new { Email = email }, ct);
            if (response.IsSuccessStatusCode) return true;

            Error = response.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable =>
                    "Email delivery is not configured on the server, so no link could be sent.",
                HttpStatusCode.BadRequest => "That does not look like a valid email address.",
                _ => $"Could not request a sign-in link ({(int)response.StatusCode})."
            };
            return false;
        }
        catch (System.Exception exception)
        {
            Error = $"Could not reach the Backend: {exception.Message}";
            return false;
        }
    }

    /// <summary>Exchanges a magic-link token for a session and persists it.</summary>
    public async Task<RedeemOutcome> RedeemAsync(string token, CancellationToken ct = default)
    {
        Error = null;
        try
        {
            using var response = await httpFactory.CreateClient(HttpClientName)
                .PostAsJsonAsync("/v1/auth/magic-links/redeem", new { Token = token }, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Error = "That sign-in link is no longer valid. Links expire and can only be used once.";
                return RedeemOutcome.Rejected;
            }
            if (!response.IsSuccessStatusCode)
            {
                Error = $"Could not complete sign-in ({(int)response.StatusCode}).";
                return RedeemOutcome.Failed;
            }

            var redeemed = await response.Content.ReadFromJsonAsync<RedeemedMagicLinkDto>(ct);
            if (redeemed is null || string.IsNullOrWhiteSpace(redeemed.SessionToken))
            {
                Error = "The server accepted the link but returned no session.";
                return RedeemOutcome.Failed;
            }

            SessionToken = redeemed.SessionToken;
            Email = redeemed.Principal.Email;
            WorkspaceId = redeemed.Principal.SelectedWorkspaceId;
            WorkspaceName = null;
            _initialized = true;
            await tokenStore.SetTokenAsync(redeemed.SessionToken, ct);
            return RedeemOutcome.Succeeded;
        }
        catch (System.Exception exception)
        {
            Error = $"Could not reach the Backend: {exception.Message}";
            return RedeemOutcome.Failed;
        }
    }

    public async Task<IReadOnlyList<WorkspaceAccessDto>> ListWorkspacesAsync(CancellationToken ct = default)
    {
        var (status, workspaces) = await FetchWorkspacesAsync(ct);
        if (status != HttpStatusCode.OK && Error is null)
            Error = $"Could not load workspaces ({(int)status}).";
        return workspaces;
    }

    public async Task<WorkspaceAccessDto?> CreateWorkspaceAsync(string name, CancellationToken ct = default)
    {
        if (!IsAuthenticated) return null;
        try
        {
            using var request = Authorized(HttpMethod.Post, "/v1/workspaces");
            request.Content = JsonContent.Create(new { Name = name });
            using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Error = $"Could not create the workspace ({(int)response.StatusCode}).";
                return null;
            }
            return await response.Content.ReadFromJsonAsync<WorkspaceAccessDto>(ct);
        }
        catch (System.Exception exception)
        {
            Error = $"Could not create the workspace: {exception.Message}";
            return null;
        }
    }

    /// <summary>
    /// Selects the active workspace. Required before any workspace-scoped route will answer: the
    /// Backend checks the principal's selection, not merely membership.
    /// </summary>
    public async Task<bool> SelectWorkspaceAsync(string workspaceId, string? name = null, CancellationToken ct = default)
    {
        if (!IsAuthenticated) return false;
        try
        {
            using var request = Authorized(
                HttpMethod.Post, $"/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/select");
            using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Error = $"Could not select that workspace ({(int)response.StatusCode}).";
                return false;
            }
            WorkspaceId = workspaceId;
            WorkspaceName = name ?? workspaceId;
            return true;
        }
        catch (System.Exception exception)
        {
            Error = $"Could not select that workspace: {exception.Message}";
            return false;
        }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        SessionToken = null;
        WorkspaceId = null;
        WorkspaceName = null;
        Email = null;
        CatalogEntryId = null;
        Error = null;
        _initialized = false;
        await tokenStore.ClearTokenAsync(ct);
    }

    /// <summary>
    /// Validates a stored token against the Backend and restores its selected workspace. A token
    /// that no longer authenticates is discarded here rather than left to fail on a later page.
    /// </summary>
    async Task<bool> AdoptTokenAsync(string token, CancellationToken ct)
    {
        SessionToken = token;
        var (status, workspaces) = await FetchWorkspacesAsync(ct);

        if (status == HttpStatusCode.Unauthorized)
        {
            SessionToken = null;
            await tokenStore.ClearTokenAsync(ct);
            return false;
        }
        if (status != HttpStatusCode.OK)
        {
            // Backend unreachable or erroring. Keep the token - it is probably still good - but
            // report the failure instead of silently falling through to a Development fixture.
            SessionToken = null;
            Error = $"Could not verify the stored session ({(int)status}).";
            return false;
        }

        // A single workspace needs no picker; selecting it keeps the common case one click.
        if (WorkspaceId is null && workspaces.Count == 1)
            await SelectWorkspaceAsync(workspaces[0].WorkspaceId, workspaces[0].Name, ct);
        else if (WorkspaceId is not null)
            WorkspaceName ??= workspaces.FirstOrDefault(w => w.WorkspaceId == WorkspaceId)?.Name ?? WorkspaceId;

        return IsAuthenticated;
    }

    async Task<(HttpStatusCode Status, IReadOnlyList<WorkspaceAccessDto> Workspaces)> FetchWorkspacesAsync(
        CancellationToken ct)
    {
        if (!IsAuthenticated) return (HttpStatusCode.Unauthorized, []);
        try
        {
            using var request = Authorized(HttpMethod.Get, "/v1/workspaces");
            using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return (response.StatusCode, []);
            var workspaces = await response.Content.ReadFromJsonAsync<List<WorkspaceAccessDto>>(ct);
            return (HttpStatusCode.OK, workspaces ?? []);
        }
        catch (System.Exception exception)
        {
            Error = $"Could not load workspaces: {exception.Message}";
            return (HttpStatusCode.ServiceUnavailable, []);
        }
    }

    HttpRequestMessage Authorized(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
        return request;
    }
}
