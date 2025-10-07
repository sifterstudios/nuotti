using Microsoft.AspNetCore.SignalR.Client;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.SimKit;

internal static class Program
{
    private const string HelpText = @"nuotti-sim

Usage:
  nuotti-sim run --backend <url> --session <code>
  nuotti-sim --help

Options:
  --backend <url>   Backend base URL (e.g., http://localhost:5240)
  --session <code>  Session code to join (e.g., dev)
  --help            Show this help
";

    private static int Main(string[] args)
    {
        if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0];
        if (string.Equals(command, "run", StringComparison.OrdinalIgnoreCase))
        {
            var backend = GetOptionValue(args, "--backend");
            var session = GetOptionValue(args, "--session");

            if (string.IsNullOrWhiteSpace(backend) || string.IsNullOrWhiteSpace(session))
            {
                Console.Error.WriteLine("Missing required options.\n");
                PrintHelp();
                return 2;
            }

            // Touch types from Contracts and SignalR so the references are meaningful
            // (these may be used by future implementations of the simulator).
            GameStateSnapshot? _exampleContractsUsage = null;
            _ = typeof(GameStateSnapshot);
            HubConnection? _exampleSignalRUsage = null;
            _ = typeof(HubConnection);
            _ = typeof(HttpClient);

            Console.WriteLine($"Simulating against backend {backend} in session '{session}'.");
            Console.WriteLine("(Simulation logic not yet implemented; this is a scaffold.)");
            return 0;
        }

        Console.Error.WriteLine($"Unknown command: {command}\n");
        PrintHelp();
        return 2;
    }

    private static bool HasFlag(string[] args, string flag)
        => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOptionValue(string[] args, string name)
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

    private static void PrintHelp() => Console.WriteLine(HelpText);
}
