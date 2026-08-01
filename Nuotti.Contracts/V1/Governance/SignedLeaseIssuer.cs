using System.Security.Cryptography;
using System.Text;

namespace Nuotti.Contracts.V1.Governance;

public sealed record SignedLease(
    string AgentId,
    string WorkspaceId,
    string SessionCode,
    DateTimeOffset ExpiresAt,
    string Signature);

/// <summary>
/// HMAC-signed Show Agent lease seam. Verifies expiry and integrity before granting control.
/// </summary>
public sealed class SignedLeaseIssuer(byte[] signingKey)
{
    public SignedLeaseIssuer(string signingKeyBase64)
        : this(Convert.FromBase64String(signingKeyBase64))
    {
    }

    public static byte[] CreateKey() => RandomNumberGenerator.GetBytes(32);

    public SignedLease Issue(string agentId, string workspaceId, string sessionCode, DateTimeOffset expiresAt)
    {
        var payload = Canonical(agentId, workspaceId, sessionCode, expiresAt);
        var signature = Sign(payload);
        return new SignedLease(agentId, workspaceId, sessionCode, expiresAt, signature);
    }

    public bool TryVerify(SignedLease lease, DateTimeOffset nowUtc, out string? reason)
    {
        if (nowUtc >= lease.ExpiresAt)
        {
            reason = "expired";
            return false;
        }

        var expected = Sign(Canonical(lease.AgentId, lease.WorkspaceId, lease.SessionCode, lease.ExpiresAt));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(lease.Signature)))
        {
            reason = "invalid-signature";
            return false;
        }

        reason = null;
        return true;
    }

    static string Canonical(string agentId, string workspaceId, string sessionCode, DateTimeOffset expiresAt)
        => $"{agentId}\n{workspaceId}\n{sessionCode}\n{expiresAt.UtcDateTime:O}";

    string Sign(string payload)
    {
        using var hmac = new HMACSHA256(signingKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
