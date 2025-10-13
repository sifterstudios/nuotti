using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Nuotti.AudioEngine;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
static string GetArg(string[] args, string name, string? envVar = null, string? fallback = null)
{
    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == $"--{name}" || args[i] == $"-{name[0]}") && i + 1 < args.Length)
            return args[i + 1];
        var prefix = $"--{name}=";
        if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return args[i].Substring(prefix.Length);
    }
    var fromEnv = !string.IsNullOrWhiteSpace(envVar) ? Environment.GetEnvironmentVariable(envVar) : null;
    return !string.IsNullOrWhiteSpace(fromEnv) ? fromEnv! : (fallback ?? string.Empty);
}

static void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}

static (string fileName, string args)? BuildPlayerCommand(string url, PreferredPlayer preferred)
{
    // Try a few simple options cross-platform. Keep it dead simple for MVP.
    var quotedUrl = '"' + url + '"';

    // Helper local function to map enum to command attempt
    static (string file, string args)? TryMap(PreferredPlayer p, string q)
    {
        return p switch
        {
            PreferredPlayer.Afplay => ("afplay", q),
            PreferredPlayer.Ffplay => ("ffplay", $"-nodisp -autoexit {q}"),
            PreferredPlayer.Vlc => ("vlc", $"--play-and-exit {q}"),
            _ => null
        };
    }

    // Ordered attempts depending on OS and preferred
    List<(string file, string args)> attempts = new();

    var pref = TryMap(preferred, quotedUrl);
    if (pref is not null) attempts.Add(pref.Value);

    if (OperatingSystem.IsMacOS())
    {
        attempts.Add(("afplay", quotedUrl));
        attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
        attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
    }
    else if (OperatingSystem.IsWindows())
    {
        attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
        attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
        // Fallback: powershell start (may open default handler)
        attempts.Add(("powershell", $"-Command Start-Process {quotedUrl}"));
    }
    else
    {
        // Linux and others: prefer ffplay, else vlc
        attempts.Add(("ffplay", $"-nodisp -autoexit {quotedUrl}"));
        attempts.Add(("vlc", $"--play-and-exit {quotedUrl}"));
    }

    foreach (var a in attempts)
    {
        if (CanStart(a.file)) return (a.file, a.args);
    }
    return null;
}

static bool CanStart(string fileName)
{
    try
    {
        // Heuristic: try to start with --version quickly and kill. If it fails with Win32Exception (not found), return false.
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (!p.Start()) return false;
        // Don't wait long; kill immediately to avoid hanging
        try { if (!p.WaitForExit(300)) p.Kill(true); } catch { /* ignore */ }
        return true;
    }
    catch (Win32Exception)
    {
        return false;
    }
    catch
    {
        // If any other error occurs, assume it's present but not cooperative with --version
        return true;
    }
}

// Load engine options from engine.json and environment (NUOTTI_ENGINE__*)
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("engine.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "NUOTTI_ENGINE__")
    .Build();
var engineOptions = new EngineOptions();
configuration.Bind(engineOptions);
engineOptions.Validate();
Log("Engine effective config:\n" + JsonSerializer.Serialize(engineOptions, new JsonSerializerOptions { WriteIndented = true }));

var backend = GetArg(args, "backend", envVar: "NUOTTI_BACKEND", fallback: "http://localhost:5240");
var session = GetArg(args, "session", envVar: "NUOTTI_SESSION", fallback: "dev");

Log($"AudioEngine starting. Backend={backend}, Session={session}");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var connection = new HubConnectionBuilder()
    .WithUrl(new Uri(new Uri(backend), "/hub"))
    .WithAutomaticReconnect()
    .Build();

connection.On<PlayTrack>("PlayTrack", cmd =>
{
    try
    {
        Log($"PlayTrack received: {cmd.FileUrl}");
        var spec = BuildPlayerCommand(cmd.FileUrl, engineOptions.PreferredPlayer);
        if (spec is null)
        {
            Log("No supported player found (afplay/ffplay/vlc). Skipping.");
            return;
        }
        var (fileName, pargs) = spec.Value;
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = pargs,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        Log($"Error attempting to play: {ex.Message}");
    }
});

try
{
    await connection.StartAsync(cts.Token);
    await connection.InvokeAsync("Join", session, "engine", null, cancellationToken: cts.Token);
    // Emit initial status: Ready
    await connection.InvokeAsync("EngineStatusChanged", session, new EngineStatusChanged(EngineStatus.Ready), cancellationToken: cts.Token);
    Log("Connected and joined session. Waiting for PlayTrack commands... Press Ctrl+C to exit.");
    await Task.Delay(-1, cts.Token);
}
catch (TaskCanceledException)
{
    // normal shutdown
}
catch (Exception ex)
{
    Log($"Fatal: {ex.Message}");
}
finally
{
    try { await connection.DisposeAsync(); } catch { }
    Log("AudioEngine stopped.");
}