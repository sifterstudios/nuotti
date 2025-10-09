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

if (string.IsNullOrWhiteSpace(backend))
{
    Console.WriteLine("Enter Backend URL (e.g. http://localhost:5000): ");
    backend = Console.ReadLine();
}

if (string.IsNullOrWhiteSpace(backend))
{
    Console.WriteLine("Backend URL is required.");
    return 1;
}

var baseUri = new Uri(backend, UriKind.Absolute);
var selector = new SessionSelectionService();

if (string.IsNullOrWhiteSpace(session))
{
    if (!string.IsNullOrWhiteSpace(selector.LastSessionCode))
    {
        Console.WriteLine($"Found last session: {selector.LastSessionCode}. Use it? (Y/n)");
        var key = Console.ReadKey(intercept: true).Key;
        Console.WriteLine();
        if (key == ConsoleKey.N) { /* fallthrough to ask */ }
        else { session = selector.LastSessionCode; }
    }

    while (string.IsNullOrWhiteSpace(session))
    {
        Console.WriteLine("Choose: [1] Enter existing session  [2] Create new session");
        var choice = Console.ReadKey(intercept: true).KeyChar;
        Console.WriteLine();
        if (choice == '1')
        {
            Console.Write("Session code: ");
            session = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(session)) { /* proceed */ }
        }
        else if (choice == '2')
        {
            Console.WriteLine("Creating new session...");
            try
            {
                session = await selector.CreateNewSessionAsync(baseUri);
                Console.WriteLine($"Created session: {session}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create session: {ex.Message}");
            }
        }
    }
}

await using var performer = new PerformerClient(baseUri, session!);

var tcs = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };

performer.ConnectedChanged += connected =>
{
    Console.WriteLine(connected ? "[Performer] Connected" : "[Performer] Disconnected");
};

await performer.EnsureConnectedAsync();
Console.WriteLine("Press Ctrl+C to exit. (Control view)");

await tcs.Task;

return 0;
