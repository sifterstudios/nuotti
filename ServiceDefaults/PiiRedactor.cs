using System.Security.Cryptography;
using System.Text;

namespace ServiceDefaults;

/// <summary>
/// PII redaction utilities for sanitizing logs.
/// Rules:
/// - Player names: Truncate or anonymize (use anonymized audienceId)
/// - File paths: Log only basename or hash (not full directory paths)
/// </summary>
public static class PiiRedactor
{
    /// <summary>
    /// Redacts a player/audience name by truncating to first initial + hash.
    /// Example: "John Doe" -> "J***"
    /// </summary>
    public static string RedactPlayerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "anonymous";
        }

        // Take first character and replace rest with asterisks (max 3 asterisks)
        var trimmed = name.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed[0] + "***";
        }

        var firstChar = trimmed[0];
        var remainingLength = Math.Min(3, trimmed.Length - 1);
        return firstChar + new string('*', remainingLength);
    }

    /// <summary>
    /// Redacts a file path by extracting only the basename or computing a hash.
    /// Example: "C:\Users\John\Documents\song.mp3" -> "song.mp3" or hash
    /// </summary>
    public static string RedactFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "unknown";
        }

        try
        {
            // Extract basename
            var fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                // If filename is too long, use a hash instead
                if (fileName.Length > 50)
                {
                    return ComputeHash(fileName);
                }
                return fileName;
            }
        }
        catch
        {
            // If path parsing fails, compute hash of the path
        }

        // Fallback: compute hash of the full path
        return ComputeHash(filePath);
    }

    /// <summary>
    /// Redacts an audience ID to use anonymized format.
    /// If it's already a connection ID (GUID), return a shortened version.
    /// </summary>
    public static string RedactAudienceId(string? audienceId)
    {
        if (string.IsNullOrWhiteSpace(audienceId))
        {
            return "unknown";
        }

        // If it's a GUID, return first 8 chars + "***"
        if (Guid.TryParse(audienceId, out _))
        {
            return audienceId.Substring(0, Math.Min(8, audienceId.Length)) + "***";
        }

        // Otherwise, use hash
        return ComputeHash(audienceId);
    }

    /// <summary>
    /// Computes a short hash of the input for anonymization.
    /// </summary>
    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        // Return first 8 characters of hex hash
        return Convert.ToHexString(hash).Substring(0, 8).ToLowerInvariant();
    }
}

