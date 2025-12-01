using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Nuotti.Backend.Diagnostics;
using Nuotti.Backend.Metrics;
using Nuotti.Backend.Sessions;
using System.Text.Json;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// Diagnostics bundle export endpoints.
/// </summary>
internal static class DiagnosticsEndpoints
{
    public static void MapDiagnosticsEndpoints(this WebApplication app)
    {
        // Export diagnostics bundle
        app.MapPost("/diagnostics/export", async (
            DiagnosticsBundleService bundleService,
            BackendMetrics metrics,
            ISessionStore sessionStore,
            IGameStateStore gameStateStore,
            IConfiguration configuration,
            [FromQuery] string? session = null,
            [FromQuery] int logFileCount = 5) =>
        {
            try
            {
                // Fetch current metrics, about, and status before creating bundle
                var aboutInfo = ServiceDefaults.VersionInfo.GetVersionInfo("Nuotti.Backend");
                var metricsSnapshot = metrics.Snapshot(sessionStore);

                // Create enhanced bundle with live data
                var bundlePath = await CreateEnhancedBundleAsync(
                    bundleService,
                    aboutInfo,
                    metricsSnapshot,
                    session,
                    gameStateStore,
                    logFileCount);

                // Return bundle
                var bundleBytes = await File.ReadAllBytesAsync(bundlePath);
                var fileName = Path.GetFileName(bundlePath);

                // Cleanup temp bundle after returning (fire and forget)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    try { File.Delete(bundlePath); } catch { }
                });

                return Results.File(
                    bundleBytes,
                    "application/zip",
                    fileName);
            }
            catch (System.Exception ex)
            {
                return Results.Problem(
                    detail: $"Failed to create diagnostics bundle: {ex.Message}",
                    statusCode: 500);
            }
        })
        .RequireCors("NuottiCors")
        .Produces<FileResult>(200, "application/zip");
    }

    private static async Task<string> CreateEnhancedBundleAsync(
        DiagnosticsBundleService bundleService,
        ServiceDefaults.VersionInfoResult aboutInfo,
        MetricsSnapshot metricsSnapshot,
        string? sessionCode,
        IGameStateStore gameStateStore,
        int logFileCount)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = string.IsNullOrWhiteSpace(sessionCode)
            ? $"nuotti-diagnostics-{timestamp}.zip"
            : $"nuotti-diagnostics-{sessionCode}-{timestamp}.zip";
        var finalPath = Path.Combine(Path.GetTempPath(), fileName);

        using var archive = System.IO.Compression.ZipFile.Open(finalPath, System.IO.Compression.ZipArchiveMode.Create);

        var includedFiles = new List<string>();

        // Add about.json
        var aboutEntry = archive.CreateEntry("about.json");
        await using var aboutStream = aboutEntry.Open();
        await using var aboutWriter = new StreamWriter(aboutStream);
        var aboutJson = JsonSerializer.Serialize(aboutInfo, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await aboutWriter.WriteAsync(aboutJson);
        includedFiles.Add("about.json");

        // Add metrics.json
        var metricsEntry = archive.CreateEntry("metrics.json");
        await using var metricsStream = metricsEntry.Open();
        await using var metricsWriter = new StreamWriter(metricsStream);
        var metricsJson = JsonSerializer.Serialize(metricsSnapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await metricsWriter.WriteAsync(metricsJson);
        includedFiles.Add("metrics.json");

        // Add status.json if session provided
        if (!string.IsNullOrWhiteSpace(sessionCode) && gameStateStore.TryGet(sessionCode, out var snapshot))
        {
            var statusEntry = archive.CreateEntry($"status-{sessionCode}.json");
            await using var statusStream = statusEntry.Open();
            await using var statusWriter = new StreamWriter(statusStream);
            var statusJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await statusWriter.WriteAsync(statusJson);
            includedFiles.Add($"status-{sessionCode}.json");
        }

        // Add redacted config (using service helper)
        var configEntry = archive.CreateEntry("config-redacted.json");
        await using var configStream = configEntry.Open();
        await using var configWriter = new StreamWriter(configStream);
        var redactedConfig = bundleService.RedactConfiguration();
        var configJson = JsonSerializer.Serialize(redactedConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await configWriter.WriteAsync(configJson);
        includedFiles.Add("config-redacted.json");

        // Add logs info (indicating where logs are located)
        var logsInfoEntry = archive.CreateEntry("logs-info.txt");
        await using var logsInfoStream = logsInfoEntry.Open();
        await using var logsInfoWriter = new StreamWriter(logsInfoStream);
        await logsInfoWriter.WriteLineAsync("Log files location:");
        await logsInfoWriter.WriteLineAsync("- Backend logs: N/A (Backend doesn't use file sink)");
        await logsInfoWriter.WriteLineAsync("- Performer logs: AppData\\Roaming\\Nuotti\\Logs\\Nuotti.Performer\\");
        await logsInfoWriter.WriteLineAsync("- AudioEngine logs: AppData\\Roaming\\Nuotti\\Logs\\Nuotti.AudioEngine\\");
        await logsInfoWriter.WriteLineAsync();
        await logsInfoWriter.WriteLineAsync($"Collect the last {logFileCount} log files from each service and add them to this bundle.");
        includedFiles.Add("logs-info.txt");

        // Add manifest
        var manifestEntry = archive.CreateEntry("manifest.json");
        await using var manifestStream = manifestEntry.Open();
        await using var manifestWriter = new StreamWriter(manifestStream);
        var manifest = new
        {
            timestamp = timestamp,
            sessionCode = sessionCode,
            service = "Nuotti.Backend",
            version = aboutInfo.Version,
            runtime = aboutInfo.Runtime,
            includedFiles = includedFiles
        };
        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await manifestWriter.WriteAsync(manifestJson);
        includedFiles.Add("manifest.json");

        return finalPath;
    }
}

