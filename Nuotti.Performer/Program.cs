using Nuotti.Performer;
string? GetArg(string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
        {
            return args[i + 1];
        }
        if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            return args[i].Substring(name.Length + 1);
        }
    }
    return null;
}

var backend = GetArg("--backend") ?? Environment.GetEnvironmentVariable("NUOTTI_BACKEND");
var session = GetArg("--session") ?? Environment.GetEnvironmentVariable("NUOTTI_SESSION");

if (string.IsNullOrWhiteSpace(backend) || string.IsNullOrWhiteSpace(session))
{
    Console.WriteLine("Usage: Nuotti.Performer --backend <url> --session <code>");
    Console.WriteLine("Or set NUOTTI_BACKEND and NUOTTI_SESSION environment variables.");
    return 1;
}

var baseUri = new Uri(backend, UriKind.Absolute);
await using var performer = new PerformerClient(baseUri, session);

var tcs = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };

performer.ConnectedChanged += connected =>
{
    Console.WriteLine(connected ? "[Performer] Connected" : "[Performer] Disconnected");
};

await performer.EnsureConnectedAsync();
Console.WriteLine("Press Ctrl+C to exit.");

await tcs.Task;

return 0;