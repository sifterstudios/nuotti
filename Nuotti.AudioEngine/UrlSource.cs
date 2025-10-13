namespace Nuotti.AudioEngine;

public static class UrlSource
{
    public enum Kind { Http, Https, File }

    public sealed record Parsed(Kind Scheme, string Normalized, string? LocalPath);

    public static bool TryNormalize(string input, out Parsed? parsed, out string? error)
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(input)) { error = "URL is required"; return false; }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            switch (uri.Scheme.ToLowerInvariant())
            {
                case "http":
                    parsed = new Parsed(Kind.Http, uri.ToString(), null);
                    return true;
                case "https":
                    parsed = new Parsed(Kind.Https, uri.ToString(), null);
                    return true;
                case "file":
                    // Normalize to full path. Uri.LocalPath decodes percent-encoding.
                    var localPath = uri.LocalPath;
                    if (string.IsNullOrWhiteSpace(localPath)) { error = "file:// URI has no path"; return false; }
                    // Ensure path is in OS format
                    var fullPath = Path.GetFullPath(localPath);
                    parsed = new Parsed(Kind.File, new Uri(fullPath).AbsoluteUri, fullPath);
                    return true;
                default:
                    error = $"Unsupported URI scheme: {uri.Scheme}";
                    return false;
            }
        }

        // If it's a plain Windows path, allow converting to file://
        if (Path.IsPathFullyQualified(input))
        {
            var full = Path.GetFullPath(input);
            var fileUri = new Uri(full).AbsoluteUri;
            parsed = new Parsed(Kind.File, fileUri, full);
            return true;
        }

        error = "Invalid URL or path format";
        return false;
    }
}
