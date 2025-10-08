using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Model;
using System.Globalization;
namespace Nuotti.SimKit;

internal static class Program
{
    const string HelpText = @"nuotti-sim

Usage:
  nuotti-sim run --backend <url> --session <code> [--preset <name>] [--audiences <n>] [--jitter <ms>] [--disconnect-rate <0..1>] [--speed <x>] [--instant]
  nuotti-sim --help

Options:
  --backend <url>         Backend base URL (e.g., http://localhost:5240)
  --session <code>        Session code to join (e.g., dev)
  --preset <name>         Scenario preset: baseline | load | chaos
  --audiences <n>         Override: number of simulated audiences (integer >= 0)
  --jitter <ms>           Override: latency jitter in milliseconds (number >= 0)
  --disconnect-rate <r>   Override: probability of random disconnect per tick (0..1)
  --speed <x>             Speed multiplier for simulation timing (e.g., 0.5 = slower, 2 = faster)
  --instant               Run with no waits (overrides --speed)
  --help                  Show this help
";

    internal enum Preset { None = 0, Baseline, Load, Chaos }

    internal sealed record RunArgs(
        string Backend,
        string Session,
        Preset Preset,
        int? Audiences,
        double? JitterMs,
        double? DisconnectRate,
        double Speed,
        bool Instant
    );

    internal static bool TryParseRunArgs(string[] args, out RunArgs? parsed, out string error)
    {
        parsed = null;
        error = string.Empty;

        var backend = GetOptionValue(args, "--backend");
        var session = GetOptionValue(args, "--session");
        if (string.IsNullOrWhiteSpace(backend) || string.IsNullOrWhiteSpace(session))
        {
            error = "Missing required options: --backend and --session";
            return false;
        }

        // preset
        Preset preset = Preset.None;
        var presetStr = GetOptionValue(args, "--preset");
        if (!string.IsNullOrWhiteSpace(presetStr))
        {
            if (presetStr.Equals("baseline", StringComparison.OrdinalIgnoreCase)) preset = Preset.Baseline;
            else if (presetStr.Equals("load", StringComparison.OrdinalIgnoreCase)) preset = Preset.Load;
            else if (presetStr.Equals("chaos", StringComparison.OrdinalIgnoreCase)) preset = Preset.Chaos;
            else { error = $"Invalid --preset '{presetStr}'. Allowed: baseline|load|chaos"; return false; }
        }

        // overrides
        int? audiences = null;
        var audStr = GetOptionValue(args, "--audiences");
        if (!string.IsNullOrWhiteSpace(audStr))
        {
            if (!int.TryParse(audStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aud) || aud < 0)
            { error = "--audiences must be a non-negative integer"; return false; }
            audiences = aud;
        }

        double? jitterMs = null;
        var jitterStr = GetOptionValue(args, "--jitter");
        if (!string.IsNullOrWhiteSpace(jitterStr))
        {
            if (!double.TryParse(jitterStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var jit) || jit < 0)
            { error = "--jitter must be a number >= 0 (milliseconds)"; return false; }
            jitterMs = jit;
        }

        double? disconnectRate = null;
        var rateStr = GetOptionValue(args, "--disconnect-rate");
        if (!string.IsNullOrWhiteSpace(rateStr))
        {
            if (!double.TryParse(rateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) || rate < 0 || rate > 1)
            { error = "--disconnect-rate must be a number in [0,1]"; return false; }
            disconnectRate = rate;
        }

        // timing
        bool instant = HasFlag(args, "--instant");
        double speed = 1.0;
        var speedStr = GetOptionValue(args, "--speed");
        if (!string.IsNullOrWhiteSpace(speedStr))
        {
            if (!double.TryParse(speedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
            { error = $"Invalid --speed value '{speedStr}'. Use a number like 0.5 or 2.0"; return false; }
            if (speed < 0) speed = 0;
        }

        parsed = new RunArgs(backend!, session!, preset, audiences, jitterMs, disconnectRate, speed, instant);
        return true;
    }

    static int Main(string[] args)
    {
        if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0];
        if (string.Equals(command, "run", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseRunArgs(args, out var run, out var error))
            {
                Console.Error.WriteLine(error + "\n");
                PrintHelp();
                return 2;
            }

            var effective = run!.Instant ? "instant (no waits)" : $"speed x{run.Speed.ToString(CultureInfo.InvariantCulture)}";

            // Touch types from Contracts and SignalR so the references are meaningful
            // (these may be used by future implementations of the simulator).
            GameStateSnapshot? _exampleContractsUsage = null;
            _ = typeof(GameStateSnapshot);
            HubConnection? _exampleSignalRUsage = null;
            _ = typeof(HubConnection);
            _ = typeof(HttpClient);

            var presetMsg = run.Preset == Preset.None ? "no preset" : $"preset {run.Preset.ToString().ToLowerInvariant()}";
            Console.WriteLine($"Simulating against backend {run.Backend} in session '{run.Session}' with {effective}, {presetMsg}.");
            if (run.Audiences is not null) Console.WriteLine($"Override: audiences={run.Audiences}");
            if (run.JitterMs is not null) Console.WriteLine($"Override: jitterMs={run.JitterMs}");
            if (run.DisconnectRate is not null) Console.WriteLine($"Override: disconnectRate={run.DisconnectRate}");
            Console.WriteLine("(Simulation logic not yet implemented; this is a scaffold.)");
            return 0;
        }

        Console.Error.WriteLine($"Unknown command: {command}\n");
        PrintHelp();
        return 2;
    }

    static bool HasFlag(string[] args, string flag)
        => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    static string? GetOptionValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    return args[i + 1];
                return null;
            }
            // also support --name=value format
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i].Substring(name.Length + 1);
            }
        }
        return null;
    }

    static void PrintHelp() => Console.WriteLine(HelpText);
}