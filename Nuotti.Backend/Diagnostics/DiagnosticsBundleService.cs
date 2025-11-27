using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Sessions;
using ServiceDefaults;

namespace Nuotti.Backend.Diagnostics;

/// <summary>
/// Service to create diagnostics bundles containing logs, metrics, status, and configuration.
/// </summary>
public class DiagnosticsBundleService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticsBundleService> _logger;

    public DiagnosticsBundleService(
        IConfiguration configuration,
        ILogger<DiagnosticsBundleService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates a diagnostics bundle ZIP file containing logs, metrics, status, about, and redacted config.
    /// </summary>
    /// <param name="sessionCode">Optional session code to include in the bundle filename.</param>
    /// <param name="logFileCount">Number of recent log files to include (default: 5).</param>
    /// <returns>Path to the created ZIP file.</returns>
    public async Task<string> CreateBundleAsync(
        string? sessionCode = null,
        int logFileCount = 5,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = string.IsNullOrWhiteSpace(sessionCode)
            ? $"nuotti-diagnostics-{timestamp}.zip"
            : $"nuotti-diagnostics-{sessionCode}-{timestamp}.zip";

        var tempDir = Path.Combine(Path.GetTempPath(), "NuottiDiagnostics", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, fileName);

        try
        {
            using var zipStream = File.Create(zipPath);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Add /about endpoint data
            await AddAboutInfoAsync(archive, cancellationToken);

            // Add /metrics endpoint data (placeholder - will be populated by endpoint)
            await AddMetricsPlaceholderAsync(archive, cancellationToken);

            // Add /status endpoint data (if session provided)
            if (!string.IsNullOrWhiteSpace(sessionCode))
            {
                await AddStatusPlaceholderAsync(archive, sessionCode, cancellationToken);
            }

            // Add redacted configuration
            await AddRedactedConfigAsync(archive, cancellationToken);

            // Add log files (if available)
            await AddLogFilesAsync(archive, logFileCount, cancellationToken);

            // Add bundle manifest
            await AddManifestAsync(archive, sessionCode, timestamp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create diagnostics bundle: {Message}", ex.Message);
            throw;
        }

        // Move ZIP to final location (user's Downloads or temp)
        var finalPath = GetFinalPath(fileName);
        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(zipPath, finalPath);

        // Cleanup temp directory
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }

        return finalPath;
    }

    private async Task AddAboutInfoAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("about.json");
        var versionInfo = VersionInfo.GetVersionInfo("Nuotti.Backend");
        var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private async Task AddMetricsPlaceholderAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("metrics-placeholder.txt");
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("Metrics endpoint data should be fetched via GET /metrics and added to this bundle.");
        await writer.WriteLineAsync("This placeholder indicates the metrics endpoint is available but was not included in the bundle.");
    }

    private async Task AddStatusPlaceholderAsync(ZipArchive archive, string sessionCode, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry($"status-{sessionCode}-placeholder.txt");
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync($"Status endpoint data for session '{sessionCode}' should be fetched via GET /status/{sessionCode} and added to this bundle.");
        await writer.WriteLineAsync("This placeholder indicates the status endpoint is available but was not included in the bundle.");
    }

    private async Task AddRedactedConfigAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("config-redacted.json");
        var redactedConfig = RedactConfiguration(_configuration);
        var json = JsonSerializer.Serialize(redactedConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private async Task AddLogFilesAsync(ZipArchive archive, int logFileCount, CancellationToken cancellationToken)
    {
        // Log files are written by Serilog file sink (J8)
        // They are stored in AppData\Roaming\Nuotti\Logs\Nuotti.Backend\
        // Since this is the Backend service, we won't have access to Performer/Engine logs
        // For now, we'll add a note that logs should be collected from the respective services
        
        var entry = archive.CreateEntry("logs-info.txt");
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("Log files location:");
        await writer.WriteLineAsync("- Backend logs: N/A (Backend doesn't use file sink)");
        await writer.WriteLineAsync("- Performer logs: AppData\\Roaming\\Nuotti\\Logs\\Nuotti.Performer\\");
        await writer.WriteLineAsync("- AudioEngine logs: AppData\\Roaming\\Nuotti\\Logs\\Nuotti.AudioEngine\\");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"Collect the last {logFileCount} log files from each service and add them to this bundle.");
    }

    private async Task AddManifestAsync(ZipArchive archive, string? sessionCode, string timestamp, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("manifest.json");
        var manifest = new
        {
            timestamp = timestamp,
            sessionCode = sessionCode,
            service = "Nuotti.Backend",
            version = VersionInfo.GetVersionInfo("Nuotti.Backend").Version,
            runtime = VersionInfo.GetVersionInfo("Nuotti.Backend").Runtime,
            includedFiles = archive.Entries.Select(e => e.FullName).ToList()
        };
        
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    public Dictionary<string, object?> RedactConfiguration()
    {
        return RedactConfiguration(_configuration);
    }

    private Dictionary<string, object?> RedactConfiguration(IConfiguration configuration)
    {
        var redacted = new Dictionary<string, object?>();
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "secret", "key", "token", "connectionstring", "connection", "api", "auth"
        };

        foreach (var kvp in configuration.AsEnumerable(makePathsRelative: false))
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;

            var key = kvp.Key;
            var value = kvp.Value;

            // Redact sensitive keys
            if (sensitiveKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                value = "***REDACTED***";
            }

            // Redact file paths (keep only basename)
            if (value != null && (key.Contains("path", StringComparison.OrdinalIgnoreCase) ||
                                 key.Contains("file", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var basename = Path.GetFileName(value);
                    if (!string.IsNullOrWhiteSpace(basename))
                    {
                        value = basename;
                    }
                }
                catch
                {
                    // Ignore path parsing errors
                }
            }

            redacted[key] = value;
        }

        return redacted;
    }

    private string GetFinalPath(string fileName)
    {
        // Try to use Downloads folder, fallback to temp
        var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(downloads))
        {
            var downloadsPath = Path.Combine(downloads, "Downloads", fileName);
            try
            {
                // Ensure Downloads directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(downloadsPath)!);
                return downloadsPath;
            }
            catch
            {
                // Fallback to temp
            }
        }

        return Path.Combine(Path.GetTempPath(), fileName);
    }
}

