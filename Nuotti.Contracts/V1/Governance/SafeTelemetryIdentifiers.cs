using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Nuotti.Contracts.V1.Governance;

/// <summary>
/// Correlates telemetry with safe identifiers and redacts PII/secrets before they leave the process.
/// </summary>
public static class SafeTelemetryIdentifiers
{
    static readonly Regex SecretPattern = new(
        @"(?i)(?:authorization\s*[=:]\s*bearer\s+[^\s,;}]+|bearer\s+[^\s,;}]+|(?:password|secret|token|apikey|api_key|authorization)\s*[=:]\s*[^\s,;}]+)",
        RegexOptions.Compiled);

    public static string CorrelateSession(string? sessionCode)
    {
        if (string.IsNullOrWhiteSpace(sessionCode)) return "session:unknown";
        return "session:" + ShortHash(sessionCode.Trim().ToUpperInvariant());
    }

    public static string CorrelateWorkspace(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId)) return "workspace:unknown";
        return "workspace:" + ShortHash(workspaceId.Trim());
    }

    public static string CorrelateActor(string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId)) return "actor:unknown";
        return "actor:" + ShortHash(actorId.Trim());
    }

    public static string RedactSecrets(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return SecretPattern.Replace(text, "***REDACTED***");
    }

    public static bool ContainsSecret(string? text)
        => !string.IsNullOrEmpty(text) && SecretPattern.IsMatch(text);

    static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}
