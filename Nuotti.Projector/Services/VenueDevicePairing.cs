using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nuotti.Projector.Services;

/// <summary>What this projector was paired to, kept between runs.</summary>
public sealed record VenueDeviceCredential(string AgentId, string Credential, string WorkspaceId, string SessionCode);

/// <summary>
/// Stores the pairing credential on disk so a venue projector is paired once, not once per boot.
/// </summary>
public sealed class VenueCredentialStore
{
    readonly string _path;

    public VenueCredentialStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nuotti.Projector", "pairing.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public VenueDeviceCredential? Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<VenueDeviceCredential>(File.ReadAllText(_path))
                : null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable pairing file is the same situation as never having paired:
            // the operator types the code again. Failing to start would be worse.
            return null;
        }
    }

    public void Save(VenueDeviceCredential credential)
        => File.WriteAllText(_path, JsonSerializer.Serialize(credential));

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

/// <summary>
/// Pairs this projector with a session and keeps a usable access token for the hub.
/// </summary>
/// <remarks>
/// The projector used to declare itself the projector of a session it named, over a hub that took
/// its word for both. It now presents a credential the band issued from inside their own
/// workspace, and the backend decides what that credential is allowed to do. The session code is
/// learned from the pairing rather than configured, so the venue machine no longer needs to be
/// told which show it is running.
/// </remarks>
public sealed class VenueDevicePairingClient(HttpClient http, VenueCredentialStore store)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim _gate = new(1, 1);
    string? _accessToken;
    DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    sealed record PairResponse(string AgentId, string Credential, string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string WorkspaceId, string SessionCode);

    public VenueDeviceCredential? Current => store.Load();

    /// <summary>Redeems an eight-digit code the band generated for this session.</summary>
    public async Task<VenueDeviceCredential?> PairAsync(string code, string deviceName, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/v1/show-agent/pair",
            new { code, name = deviceName }, Json, ct);
        if (!response.IsSuccessStatusCode) return null;
        var paired = await response.Content.ReadFromJsonAsync<PairResponse>(Json, ct);
        if (paired is null) return null;

        // The pair response does not say which workspace or session the code belonged to; the
        // token exchange does. Doing it now means the very first connection already knows which
        // show it is joining.
        var exchange = await ExchangeAsync(paired.Credential, ct);
        if (exchange.Outcome != ExchangeOutcome.Ok || exchange.Lease is null) return null;
        var lease = exchange.Lease;

        var credential = new VenueDeviceCredential(paired.AgentId, paired.Credential, lease.WorkspaceId, lease.SessionCode);
        store.Save(credential);

        // Seed the cache: the connection that follows pairing would otherwise exchange again
        // immediately for a token this call already holds.
        _accessToken = lease.AccessToken;
        _expiresAt = lease.ExpiresAt;
        return credential;
    }

    /// <summary>
    /// A currently valid hub access token, refreshing the short-lived lease when needed.
    /// </summary>
    /// <remarks>
    /// Only a definitive refusal (401/404) means the pairing is dead. A gateway blip or 5xx must
    /// not wipe the stored credential — that is what turns a transient API outage into two hub
    /// connections that present nothing and get refused forever.
    /// </remarks>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && _expiresAt > DateTimeOffset.UtcNow.AddSeconds(30)) return _accessToken;

            var credential = store.Load();
            if (credential is null) return null;

            var exchange = await ExchangeAsync(credential.Credential, ct);
            if (exchange.Outcome == ExchangeOutcome.Revoked)
            {
                // The band revoked this device, or the session is over. Forget the credential so
                // the projector asks to be paired again instead of retrying a dead lease forever.
                store.Delete();
                _accessToken = null;
                return null;
            }

            if (exchange.Outcome != ExchangeOutcome.Ok || exchange.Lease is null)
                return null;

            var lease = exchange.Lease;
            _accessToken = lease.AccessToken;
            _expiresAt = lease.ExpiresAt;
            if (lease.SessionCode != credential.SessionCode || lease.WorkspaceId != credential.WorkspaceId)
                store.Save(credential with { SessionCode = lease.SessionCode, WorkspaceId = lease.WorkspaceId });
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task<(ExchangeOutcome Outcome, TokenResponse? Lease)> ExchangeAsync(string credential, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/v1/show-agent/token", new { credential }, Json, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            return (ExchangeOutcome.Revoked, null);
        if (!response.IsSuccessStatusCode)
            return (ExchangeOutcome.TransientFailure, null);
        var lease = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, ct);
        return lease is null
            ? (ExchangeOutcome.TransientFailure, null)
            : (ExchangeOutcome.Ok, lease);
    }

    enum ExchangeOutcome { Ok, Revoked, TransientFailure }
}
