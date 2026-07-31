using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;
using Nuotti.AsioHil;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => RunAsync(args).GetAwaiter().GetResult();

    private static async Task<int> RunAsync(string[] args)
    {
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Nuotti ASIO HIL requires Windows.");
    return 2;
}

if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
{
    foreach (var name in AsioOut.GetDriverNames())
    {
        try
        {
            using var candidate = new AsioOut(name);
            Console.WriteLine($"{name} | outputs={candidate.DriverOutputChannelCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{name} | unavailable={ex.Message}");
        }
    }
    return 0;
}

string? Value(string name) => args.SkipWhile(x => !x.Equals(name, StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
var driver = Value("--driver");
if (string.IsNullOrWhiteSpace(driver))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Nuotti.AsioHil -- --list | --driver <name> [--duration 10] [--offset-ms 1000] [--lead-ms 750] [--expected-buffer 256] [--report report.json]");
    return 2;
}
if (!args.Contains("--confirm-output", StringComparer.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Refusing to energize physical outputs. Check routing and levels, then repeat with --confirm-output.");
    return 2;
}

var sampleRate = int.TryParse(Value("--sample-rate"), out var sr) ? sr : 48_000;
var durationSeconds = double.TryParse(Value("--duration"), out var ds) ? ds : 10;
var offsetMs = double.TryParse(Value("--offset-ms"), out var om) ? om : 1_000;
var leadMs = double.TryParse(Value("--lead-ms"), out var lm) ? lm : 750;
var expectedBuffer = int.TryParse(Value("--expected-buffer"), out var eb) ? eb : (int?)null;
var reportPath = Value("--report") ?? $"asio-hil-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
var mode = Value("--mode")?.Equals("mono", StringComparison.OrdinalIgnoreCase) == true
    ? HilSignalMode.MonoBackingAndClick
    : HilSignalMode.StereoBackingAndClick;

using var asio = new AsioOut(driver);
var clock = Stopwatch.StartNew();
var signal = new HilSignal(sampleRate, (long)(sampleRate * durationSeconds), (long)(sampleRate * offsetMs / 1000), mode);
var provider = new HilWaveProvider(signal, clock);
if (asio.DriverOutputChannelCount < signal.Channels)
    throw new InvalidOperationException($"Driver exposes {asio.DriverOutputChannelCount} output channels; {signal.Channels} are required for {mode}.");
asio.Init(provider);

var scheduledAt = clock.Elapsed + TimeSpan.FromMilliseconds(leadMs);
await Task.Delay(TimeSpan.FromMilliseconds(leadMs));
var playCalledAt = clock.Elapsed;
asio.Play();
while (signal.FramePosition < sampleRate * durationSeconds) await Task.Delay(20);
asio.Stop();
var stoppedAt = clock.Elapsed;

var report = new
{
    schemaVersion = 1,
    capturedAtUtc = DateTimeOffset.UtcNow,
    driver,
    operatingSystem = Environment.OSVersion.VersionString,
    sampleRate,
    mode = mode.ToString(),
    channels = signal.Channels,
    asioOutputChannels = asio.NumberOfOutputChannels,
    framesPerBuffer = asio.FramesPerBuffer,
    expectedBuffer,
    bufferMatchesExpectation = expectedBuffer is null || expectedBuffer == asio.FramesPerBuffer,
    durationSeconds,
    backingOffsetMs = offsetMs,
    scheduledLeadMs = leadMs,
    playCallErrorMs = (playCalledAt - scheduledAt).TotalMilliseconds,
    firstCallbackErrorMs = provider.FirstCallbackAt is null ? (double?)null : (provider.FirstCallbackAt.Value - scheduledAt).TotalMilliseconds,
    stoppedAtMs = stoppedAt.TotalMilliseconds,
    signal = mode == HilSignalMode.MonoBackingAndClick
        ? new { backingLeftChannel = 1, backingRightChannel = (int?)null, clickChannel = 2, markerIntervalMs = 1000 }
        : new { backingLeftChannel = 1, backingRightChannel = (int?)2, clickChannel = 3, markerIntervalMs = 1000 },
    hilAcceptancePassed = false,
    acceptanceStatus = "physical-capture-required",
    physicalCapture = new { status = "pending", note = $"Loop outputs 1-{signal.Channels} into a multichannel recorder and attach/analyze the capture before claiming HIL acceptance." }
};
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(Path.GetFullPath(reportPath));
Console.Error.WriteLine("Signal emission completed; HIL acceptance remains pending physical capture and analysis.");
return report.bufferMatchesExpectation ? 3 : 1;
    }
}
