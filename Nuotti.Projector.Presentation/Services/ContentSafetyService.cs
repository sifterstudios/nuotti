using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nuotti.Projector.Services;

public class ContentSafetyService
{
    private int _maxStringLength = 500;
    private int _maxChoiceLength = 200;
    private int _maxPlayerNameLength = 50;
    private int _maxSongTitleLength = 100;
    private int _maxArtistNameLength = 80;
    
    // Patterns for potentially dangerous content
    private readonly Regex _htmlTagPattern = new(@"<[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _scriptPattern = new(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private readonly Regex _urlPattern = new(@"https?://[^\s<>""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _excessiveWhitespacePattern = new(@"\s{3,}", RegexOptions.Compiled);
    private readonly Regex _controlCharPattern = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);
    
    // Common injection patterns
    private readonly string[] _suspiciousPatterns = new[]
    {
        "javascript:",
        "data:",
        "vbscript:",
        "onload=",
        "onerror=",
        "onclick=",
        "onmouseover=",
        "eval(",
        "expression(",
        "url(",
        "import(",
        "document.",
        "window.",
        "alert(",
        "confirm(",
        "prompt("
    };
    
    public ContentSafetyResult SanitizeText(string? input, ContentType contentType = ContentType.General)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new ContentSafetyResult(string.Empty, false, "Empty input");
        }
        
        var maxLength = GetMaxLengthForType(contentType);
        var sanitized = input;
        var warnings = new List<string>();
        bool wasModified = false;
        
        // Step 1: Length clamping
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength - 3) + "...";
            warnings.Add($"Content truncated to {maxLength} characters");
            wasModified = true;
        }
        
        // Step 2: Remove control characters
        var controlCharMatch = _controlCharPattern.Match(sanitized);
        if (controlCharMatch.Success)
        {
            sanitized = _controlCharPattern.Replace(sanitized, "");
            warnings.Add("Control characters removed");
            wasModified = true;
        }
        
        // Step 3: HTML/Script sanitization
        if (_scriptPattern.IsMatch(sanitized))
        {
            sanitized = _scriptPattern.Replace(sanitized, "[SCRIPT REMOVED]");
            warnings.Add("Script tags removed for security");
            wasModified = true;
        }
        
        if (_htmlTagPattern.IsMatch(sanitized))
        {
            sanitized = _htmlTagPattern.Replace(sanitized, "");
            warnings.Add("HTML tags removed");
            wasModified = true;
        }
        
        // Step 4: Check for suspicious patterns
        foreach (var pattern in _suspiciousPatterns)
        {
            if (sanitized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Replace(pattern, "[FILTERED]", StringComparison.OrdinalIgnoreCase);
                warnings.Add($"Suspicious pattern '{pattern}' filtered");
                wasModified = true;
            }
        }
        
        // Step 5: URL sanitization (optional - might be too aggressive for some content)
        if (contentType != ContentType.SongTitle && contentType != ContentType.ArtistName)
        {
            var urlMatches = _urlPattern.Matches(sanitized);
            if (urlMatches.Count > 0)
            {
                foreach (Match match in urlMatches)
                {
                    sanitized = sanitized.Replace(match.Value, "[URL REMOVED]");
                }
                warnings.Add("URLs removed for security");
                wasModified = true;
            }
        }
        
        // Step 6: Normalize whitespace
        if (_excessiveWhitespacePattern.IsMatch(sanitized))
        {
            sanitized = _excessiveWhitespacePattern.Replace(sanitized, " ");
            warnings.Add("Excessive whitespace normalized");
            wasModified = true;
        }
        
        // Step 7: Trim and final cleanup
        sanitized = sanitized.Trim();
        
        // Step 8: Ensure we don't return empty strings for required content
        if (string.IsNullOrWhiteSpace(sanitized) && !string.IsNullOrWhiteSpace(input))
        {
            sanitized = "[CONTENT FILTERED]";
            warnings.Add("Content was completely filtered, placeholder added");
            wasModified = true;
        }
        
        return new ContentSafetyResult(sanitized, wasModified, string.Join("; ", warnings));
    }
    
    public ContentSafetyResult SanitizeChoice(string? choice, int index)
    {
        var result = SanitizeText(choice, ContentType.Choice);
        
        // Ensure choices have some content
        if (string.IsNullOrWhiteSpace(result.SafeContent))
        {
            result = new ContentSafetyResult($"Choice {index + 1}", true, "Empty choice replaced with placeholder");
        }
        
        return result;
    }
    
    public ContentSafetyResult SanitizePlayerName(string? playerName)
    {
        var result = SanitizeText(playerName, ContentType.PlayerName);
        
        // Ensure player names have some content
        if (string.IsNullOrWhiteSpace(result.SafeContent))
        {
            result = new ContentSafetyResult("Anonymous", true, "Empty player name replaced with 'Anonymous'");
        }
        
        return result;
    }
    
    public ContentSafetyResult SanitizeSongTitle(string? title)
    {
        var result = SanitizeText(title, ContentType.SongTitle);
        
        // Ensure song titles have some content
        if (string.IsNullOrWhiteSpace(result.SafeContent))
        {
            result = new ContentSafetyResult("Unknown Song", true, "Empty song title replaced with 'Unknown Song'");
        }
        
        return result;
    }
    
    public ContentSafetyResult SanitizeArtistName(string? artist)
    {
        var result = SanitizeText(artist, ContentType.ArtistName);
        
        // Artist can be empty, but if it exists, it should be clean
        return result;
    }
    
    public ContentSafetyResult SanitizeHint(string? hint)
    {
        var result = SanitizeText(hint, ContentType.General);
        
        // Hints can be empty
        return result;
    }
    
    public bool IsContentSafe(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return true;
        
        // Quick safety check without modification
        return !_scriptPattern.IsMatch(input) &&
               !_suspiciousPatterns.Any(pattern => input.Contains(pattern, StringComparison.OrdinalIgnoreCase)) &&
               !_controlCharPattern.IsMatch(input);
    }
    
    public ContentSafetyReport GenerateReport(Dictionary<string, string> content)
    {
        var report = new ContentSafetyReport();
        
        foreach (var kvp in content)
        {
            var result = SanitizeText(kvp.Value);
            if (result.WasModified)
            {
                report.ModifiedContent[kvp.Key] = result;
            }
        }
        
        return report;
    }
    
    private int GetMaxLengthForType(ContentType contentType)
    {
        return contentType switch
        {
            ContentType.Choice => _maxChoiceLength,
            ContentType.PlayerName => _maxPlayerNameLength,
            ContentType.SongTitle => _maxSongTitleLength,
            ContentType.ArtistName => _maxArtistNameLength,
            ContentType.General => _maxStringLength,
            _ => _maxStringLength
        };
    }
    
    // Configuration methods
    public void SetMaxLength(ContentType contentType, int maxLength)
    {
        switch (contentType)
        {
            case ContentType.Choice:
                _maxChoiceLength = Math.Max(10, Math.Min(maxLength, 500));
                break;
            case ContentType.PlayerName:
                _maxPlayerNameLength = Math.Max(3, Math.Min(maxLength, 100));
                break;
            case ContentType.SongTitle:
                _maxSongTitleLength = Math.Max(5, Math.Min(maxLength, 200));
                break;
            case ContentType.ArtistName:
                _maxArtistNameLength = Math.Max(3, Math.Min(maxLength, 150));
                break;
            case ContentType.General:
                _maxStringLength = Math.Max(10, Math.Min(maxLength, 1000));
                break;
        }
    }
}

public record ContentSafetyResult(string SafeContent, bool WasModified, string Warnings);

public class ContentSafetyReport
{
    public Dictionary<string, ContentSafetyResult> ModifiedContent { get; } = new();
    public bool HasModifications => ModifiedContent.Count > 0;
    public int ModificationCount => ModifiedContent.Count;
}

public enum ContentType
{
    General,
    Choice,
    PlayerName,
    SongTitle,
    ArtistName
}
