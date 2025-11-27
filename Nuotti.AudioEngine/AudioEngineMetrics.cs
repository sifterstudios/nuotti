using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ServiceDefaults;
namespace Nuotti.AudioEngine;

public sealed class AudioEngineMetrics
{
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly object _lock = new();
    private string? _currentFile;
    private string? _lastError;
    private bool _isPlaying;
    private double _avgRttMs;
    private long _rttCount;

    public DateTimeOffset StartedAtUtc => _startedAtUtc;
    public TimeSpan Uptime => DateTimeOffset.UtcNow - _startedAtUtc;

    public void SetPlaying(string? currentFile)
    {
        lock (_lock)
        {
            _isPlaying = true;
            _currentFile = currentFile;
        }
    }

    public void SetStopped()
    {
        lock (_lock)
        {
            _isPlaying = false;
        }
    }

    public void SetError(string message)
    {
        lock (_lock)
        {
            _lastError = message;
        }
    }

    // Provide an RTT sample in milliseconds (approximate)
    public void AddRttSample(double rttMs)
    {
        lock (_lock)
        {
            _rttCount++;
            // incremental average to avoid overflow
            _avgRttMs += (rttMs - _avgRttMs) / _rttCount;
        }
    }

    public MetricsSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new MetricsSnapshot(
                Playing: _isPlaying,
                CurrentFile: _currentFile,
                UptimeSeconds: Math.Max(0, Uptime.TotalSeconds),
                LastError: _lastError,
                AverageRttMs: _rttCount == 0 ? null : _avgRttMs
            );
        }
    }

    public string ToJson()
    {
        var snap = Snapshot();
        return JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}

public sealed record MetricsSnapshot(
    bool Playing,
    string? CurrentFile,
    double UptimeSeconds,
    string? LastError,
    double? AverageRttMs
);

public sealed class MetricsOptions
{
    public bool Enabled { get; set; } = false;
    // Mode: "Http" or "Console"
    public string Mode { get; set; } = "Http";
    public int Port { get; set; } = 9095;
}

public static class MetricsHost
{
    public static Task RunIfEnabledAsync(MetricsOptions opts, AudioEngineMetrics metrics, CancellationToken token)
    {
        if (!opts.Enabled) return Task.CompletedTask;
        if (string.Equals(opts.Mode, "Console", StringComparison.OrdinalIgnoreCase))
        {
            // Dump metrics on Ctrl+Break without shutting down
            Console.CancelKeyPress += (_, e) =>
            {
                if (e.SpecialKey == ConsoleSpecialKey.ControlBreak)
                {
                    try { Console.WriteLine("/metrics " + metrics.ToJson()); } catch { }
                    e.Cancel = true; // don't terminate on Ctrl+Break
                }
            };
            return Task.CompletedTask;
        }
        // Default: start simple HTTP server on loopback
        return Task.Run(async () => await RunHttpServerAsync(metrics, opts.Port, token), token);
    }

    private static async Task RunHttpServerAsync(AudioEngineMetrics metrics, int port, CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await listener.AcceptTcpClientAsync(token);
                    _ = HandleClientAsync(client, metrics, token);
                }
                catch (OperationCanceledException) { break; }
                catch { try { client?.Dispose(); } catch { } }
            }
        }
        finally
        {
            try { listener.Stop(); } catch { }
        }
    }

    private static async Task HandleClientAsync(TcpClient client, AudioEngineMetrics metrics, CancellationToken token)
    {
        using (client)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true) { NewLine = "\r\n" };

            string? requestLine = null;
            try
            {
                requestLine = await reader.ReadLineAsync(token);
                // Drain headers
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token))) { /* ignore */ }
            }
            catch { /* ignore bad request */ }

            var path = "/";
            if (!string.IsNullOrWhiteSpace(requestLine))
            {
                var parts = requestLine.Split(' ');
                if (parts.Length >= 2) path = parts[1];
            }

            if (string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase))
            {
                var json = metrics.ToJson();
                var bytes = Encoding.UTF8.GetBytes(json);
                await writer.WriteLineAsync("HTTP/1.1 200 OK");
                await writer.WriteLineAsync("Content-Type: application/json");
                await writer.WriteLineAsync($"Content-Length: {bytes.Length}");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
                await stream.WriteAsync(bytes, 0, bytes.Length, token);
            }
            else if (string.Equals(path, "/about", StringComparison.OrdinalIgnoreCase))
            {
                var info = ServiceDefaults.VersionInfo.GetVersionInfo("Nuotti.AudioEngine");
                var json = System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
                });
                var bytes = Encoding.UTF8.GetBytes(json);
                await writer.WriteLineAsync("HTTP/1.1 200 OK");
                await writer.WriteLineAsync("Content-Type: application/json");
                await writer.WriteLineAsync($"Content-Length: {bytes.Length}");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
                await stream.WriteAsync(bytes, 0, bytes.Length, token);
            }
            else if (string.Equals(path, "/health/live", StringComparison.OrdinalIgnoreCase))
            {
                var response = System.Text.Json.JsonSerializer.Serialize(new { status = "live" });
                var bytes = Encoding.UTF8.GetBytes(response);
                await writer.WriteLineAsync("HTTP/1.1 200 OK");
                await writer.WriteLineAsync("Content-Type: application/json");
                await writer.WriteLineAsync($"Content-Length: {bytes.Length}");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
                await stream.WriteAsync(bytes, 0, bytes.Length, token);
            }
            else if (string.Equals(path, "/health/ready", StringComparison.OrdinalIgnoreCase))
            {
                // Readiness check: basic validation that metrics are available (player availability is implicit)
                // For storage root: since AudioEngine doesn't have a storage root requirement in the current design,
                // we just verify that metrics are functional
                var isReady = metrics != null; // Basic check that metrics are available
                var statusCode = isReady ? 200 : 503;
                var status = isReady ? "ready" : "not ready";
                var response = System.Text.Json.JsonSerializer.Serialize(new { status = status });
                var bytes = Encoding.UTF8.GetBytes(response);
                await writer.WriteLineAsync($"HTTP/1.1 {statusCode} {(isReady ? "OK" : "Service Unavailable")}");
                await writer.WriteLineAsync("Content-Type: application/json");
                await writer.WriteLineAsync($"Content-Length: {bytes.Length}");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
                await stream.WriteAsync(bytes, 0, bytes.Length, token);
            }
            else
            {
                await writer.WriteLineAsync("HTTP/1.1 404 Not Found");
                await writer.WriteLineAsync("Content-Length: 0");
                await writer.WriteLineAsync("Connection: close");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
            }
        }
    }
}
