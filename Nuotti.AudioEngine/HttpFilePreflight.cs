using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using System.Net;
namespace Nuotti.AudioEngine;

public sealed class HttpFilePreflight : ISourcePreflight
{
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly AudioEngineSafetyOptions _options;

    public HttpFilePreflight(HttpClient httpClient, TimeSpan? timeout = null, AudioEngineSafetyOptions? options = null)
    {
        _http = httpClient;
        _timeout = timeout ?? TimeSpan.FromSeconds(3);
        _options = options ?? new AudioEngineSafetyOptions();
    }

    public async Task<PreflightResult> CheckAsync(string input, CancellationToken cancellationToken = default)
    {
        if (!UrlSource.TryNormalize(input, out var parsed, out var error))
        {
            var problem = NuottiProblem.BadRequest("Invalid URL", error ?? "Unsupported input", ReasonCode.None, "url");
            return new PreflightResult(false, null, problem);
        }

        switch (parsed!.Scheme)
        {
            case UrlSource.Kind.File:
                // Allowed-roots guard (optional)
                if (_options.AllowedRoots is { Length: > 0 })
                {
                    var okUnder = false;
                    try
                    {
                        var full = Path.GetFullPath(parsed.LocalPath!);
                        foreach (var r in _options.AllowedRoots!)
                        {
                            if (string.IsNullOrWhiteSpace(r)) continue;
                            var rootFull = Path.GetFullPath(r);
                            // Ensure trailing separator for proper prefix match
                            if (!rootFull.EndsWith(Path.DirectorySeparatorChar)) rootFull += Path.DirectorySeparatorChar;
                            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) { okUnder = true; break; }
                        }
                    }
                    catch
                    {
                        okUnder = false;
                    }
                    if (!okUnder)
                    {
                        var p = NuottiProblem.UnprocessableEntity("Path not allowed", $"Path '{parsed.LocalPath}' is outside allowed roots.", ReasonCode.None, "url");
                        return new PreflightResult(false, null, p);
                    }
                }

                // Check existence and readability
                if (string.IsNullOrWhiteSpace(parsed.LocalPath) || !File.Exists(parsed.LocalPath))
                {
                    var p = NuottiProblem.UnprocessableEntity("File not found", $"Path '{parsed.LocalPath ?? "(null)"}' does not exist.", ReasonCode.None, "url");
                    return new PreflightResult(false, null, p);
                }
                try
                {
                    using var fs = new FileStream(parsed.LocalPath!, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                catch (UnauthorizedAccessException)
                {
                    var p = new NuottiProblem("Access denied", 403, $"Cannot read file '{parsed.LocalPath}'.", ReasonCode.None, "url");
                    return new PreflightResult(false, null, p);
                }
                catch (Exception ex)
                {
                    var p = NuottiProblem.UnprocessableEntity("File not readable", ex.Message, ReasonCode.None, "url");
                    return new PreflightResult(false, null, p);
                }
                return new PreflightResult(true, parsed.Normalized, null);

            case UrlSource.Kind.Http:
            case UrlSource.Kind.Https:
                // Try a HEAD when possible
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, parsed.Normalized);
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(_timeout);
                    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300)
                    {
                        // Optional size guard
                        if (_options.MaxHttpSizeMB.HasValue && resp.Content?.Headers?.ContentLength is long len)
                        {
                            var maxBytes = (long)_options.MaxHttpSizeMB.Value * 1024L * 1024L;
                            if (len > maxBytes)
                            {
                                var p = NuottiProblem.UnprocessableEntity("Source too large", $"Content-Length {len} bytes exceeds limit {_options.MaxHttpSizeMB.Value} MB.", ReasonCode.None, "url");
                                return new PreflightResult(false, null, p);
                            }
                        }
                        return new PreflightResult(true, parsed.Normalized, null);
                    }
                    if (resp.StatusCode == HttpStatusCode.MethodNotAllowed || resp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // Can't HEAD; size unknown; allow play to proceed per requirements
                        return new PreflightResult(true, parsed.Normalized, null);
                    }
                    var prob = new NuottiProblem("Source not reachable", (int)resp.StatusCode, $"HEAD {parsed.Normalized} returned {(int)resp.StatusCode}", ReasonCode.None, "url");
                    return new PreflightResult(false, null, prob);
                }
                catch (TaskCanceledException)
                {
                    var p = NuottiProblem.UnprocessableEntity("Source timeout", "HEAD request timed out", ReasonCode.None, "url");
                    return new PreflightResult(false, null, p);
                }
                catch (HttpRequestException ex)
                {
                    var p = NuottiProblem.UnprocessableEntity("Source unreachable", ex.Message, ReasonCode.None, "url");
                    return new PreflightResult(false, null, p);
                }
                default:
                    // Should not happen due to TryNormalize
                    var pdef = NuottiProblem.BadRequest("Unsupported scheme", "Only http(s) and file:// are supported", ReasonCode.None, "url");
                    return new PreflightResult(false, null, pdef);
        }
    }
}
