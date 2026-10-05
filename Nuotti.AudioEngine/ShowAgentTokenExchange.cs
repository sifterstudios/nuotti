using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nuotti.AudioEngine;

/// <summary>Outcome of exchanging a Show Agent credential for a short-lived lease.</summary>
public enum ShowAgentLeaseOutcome
{
    Ok,
    /// <summary>Definitive refusal (401/404). The stored credential should be forgotten.</summary>
    Revoked,
    /// <summary>Gateway blip or other non-success. Keep the credential and retry later.</summary>
    TransientFailure
}

/// <summary>
/// Shared token exchange for Venue and headless Show Agent. One place owns revoked-vs-transient
/// semantics so a 5xx cannot wipe a pairing on one client while the other keeps it.
/// </summary>
public static class ShowAgentTokenExchange
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<(ShowAgentLeaseOutcome Outcome, CloudAgentLease? Lease)> ExchangeAsync(
        HttpClient http, string credential, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/v1/show-agent/token", new { credential }, Json, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            return (ShowAgentLeaseOutcome.Revoked, null);
        if (!response.IsSuccessStatusCode)
            return (ShowAgentLeaseOutcome.TransientFailure, null);
        var lease = await response.Content.ReadFromJsonAsync<CloudAgentLease>(Json, ct);
        return lease is null
            ? (ShowAgentLeaseOutcome.TransientFailure, null)
            : (ShowAgentLeaseOutcome.Ok, lease);
    }
}
