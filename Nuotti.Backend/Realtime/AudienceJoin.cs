using Nuotti.Backend.Participants;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Nuotti.Backend.Realtime;

/// <summary>An audience member's identity within one session, and the token that proves it.</summary>
public sealed record AudienceJoinTicket(string ParticipantId, string SessionCode, string Token, DateTimeOffset ExpiresAt);

/// <summary>The resolved identity behind an audience token.</summary>
public sealed record AudienceParticipantIdentity(string ParticipantId, string SessionCode);

/// <summary>
/// Issues and validates the short-lived tokens anonymous phones use to reach a session.
/// </summary>
/// <remarks>
/// Audience members have no account and never will - joining a quiz should cost one scan of a
/// code. They still need a credential rather than a bare session code, so that a participant can
/// be rate limited, moderated, revoked, and recognised again after their phone drops off wifi
/// mid-song. The device secret is what survives a reconnect: same secret, same participant.
/// </remarks>
public interface IAudienceJoinStore
{
    Task<AudienceJoinTicket> JoinAsync(string sessionCode, string deviceSecret, string? displayName = null,
        CancellationToken cancellationToken = default);
    Task<AudienceParticipantIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(string sessionCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mints tokens for participants that <see cref="IParticipantIdentityStore"/> owns.
/// </summary>
/// <remarks>
/// It deliberately does not keep its own device-to-participant map. Two stores minting ids from
/// the same device secret would give one phone two identities, and its answers and score would
/// land under whichever it happened to arrive by.
/// </remarks>
public sealed class InMemoryAudienceJoinStore(
    IParticipantIdentityStore participants, TimeProvider? timeProvider = null) : IAudienceJoinStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    // token hash -> ticket. Tokens are stored hashed for the same reason magic links are: a
    // database or log leak must not hand somebody a working session.
    readonly ConcurrentDictionary<string, AudienceParticipantIdentity> _byTokenHash = new();
    readonly ConcurrentDictionary<string, DateTimeOffset> _expiry = new();

    public Task<AudienceJoinTicket> JoinAsync(string sessionCode, string deviceSecret, string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSession = sessionCode.Trim();
        var participant = participants.JoinOrRestore(normalizedSession, deviceSecret, displayName);

        var raw = NewToken();
        var expires = _time.GetUtcNow().AddHours(8); // comfortably longer than any single show
        _byTokenHash[Hash(raw)] = new AudienceParticipantIdentity(participant.ParticipantId, normalizedSession);
        _expiry[Hash(raw)] = expires;

        return Task.FromResult(new AudienceJoinTicket(participant.ParticipantId, normalizedSession, raw, expires));
    }

    public Task<AudienceParticipantIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        if (!_byTokenHash.TryGetValue(hash, out var identity)) return Task.FromResult<AudienceParticipantIdentity?>(null);
        if (!_expiry.TryGetValue(hash, out var expires) || expires <= _time.GetUtcNow())
        {
            _byTokenHash.TryRemove(hash, out _);
            _expiry.TryRemove(hash, out _);
            return Task.FromResult<AudienceParticipantIdentity?>(null);
        }
        return Task.FromResult<AudienceParticipantIdentity?>(identity);
    }

    public Task RevokeSessionAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        foreach (var pair in _byTokenHash.Where(p => string.Equals(p.Value.SessionCode, sessionCode, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _byTokenHash.TryRemove(pair.Key, out _);
            _expiry.TryRemove(pair.Key, out _);
        }
        return Task.CompletedTask;
    }


    static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
