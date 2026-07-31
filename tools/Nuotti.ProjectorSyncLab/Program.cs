using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EngineAnchorSource>();
var app = builder.Build();

var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(app.Environment.ContentRootPath);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files });

app.MapPost("/api/start", (StartRequest request, EngineAnchorSource source) =>
    Results.Ok(source.Start(request.LeadMs is >= 100 and <= 2_000 ? request.LeadMs : 750)));
app.MapPost("/api/drift/{milliseconds:int}", (int milliseconds, EngineAnchorSource source) =>
    Results.Ok(source.InjectDrift(milliseconds)));
app.MapGet("/api/anchors", async (HttpContext context, EngineAnchorSource source) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    await foreach (var anchor in source.Stream(context.RequestAborted))
    {
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(anchor)}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
});

app.Run();

public sealed record StartRequest(int LeadMs);
public sealed record StartReport(string PlaybackInstanceId, DateTimeOffset CommandReceivedUtc, DateTimeOffset PlannedStartUtc, int LeadMs, int SampleRate, int FramesPerBuffer);
public sealed record BrowserPlaybackAnchor(string PlaybackInstanceId, string SongPackageRevisionId, int SampleRate, long Frame, long EngineMonotonicTicks, DateTimeOffset BackendUtcCorrelation, string State, double Rate, long Sequence, long ControlGeneration);

public sealed class EngineAnchorSource
{
    const int SampleRate = 48_000;
    const int FramesPerBuffer = 128;
    readonly object _gate = new();
    string _instance = "not-started";
    DateTimeOffset _plannedStartUtc;
    DateTimeOffset? _actualStartUtc;
    long _actualStartTimestamp;
    long _sequence;
    long _frameBias;

    public StartReport Start(int leadMs)
    {
        var received = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            _instance = Guid.NewGuid().ToString("N");
            _plannedStartUtc = received.AddMilliseconds(leadMs);
            _actualStartUtc = null;
            _actualStartTimestamp = 0;
            _sequence = 0;
            _frameBias = 0;
            _ = BeginAtPlannedStart(_instance, _plannedStartUtc);
            return new StartReport(_instance, received, _plannedStartUtc, leadMs, SampleRate, FramesPerBuffer);
        }
    }

    public object InjectDrift(int milliseconds)
    {
        if (milliseconds is < -500 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        lock (_gate)
        {
            _frameBias = milliseconds * SampleRate / 1_000;
            return new { injectedMilliseconds = milliseconds, frameBias = _frameBias };
        }
    }

    public async IAsyncEnumerable<BrowserPlaybackAnchor> Stream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            BrowserPlaybackAnchor? anchor;
            lock (_gate)
            {
                anchor = CreateAnchor();
            }

            if (anchor is not null)
            {
                yield return anchor;
            }

            await Task.Delay(500, cancellationToken);
        }
    }

    async Task BeginAtPlannedStart(string instance, DateTimeOffset plannedStart)
    {
        var delay = plannedStart - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        lock (_gate)
        {
            if (_instance != instance)
            {
                return;
            }

            _actualStartTimestamp = Stopwatch.GetTimestamp();
            _actualStartUtc = DateTimeOffset.UtcNow;
        }
    }

    BrowserPlaybackAnchor? CreateAnchor()
    {
        if (_instance == "not-started")
        {
            return null;
        }

        _sequence++;
        if (_actualStartUtc is null)
        {
            return new BrowserPlaybackAnchor(_instance, "sync-lab-song-r1", SampleRate, 0,
                Stopwatch.GetTimestamp(), _plannedStartUtc, "Scheduled", 0, _sequence, 1);
        }

        var elapsedSeconds = Stopwatch.GetElapsedTime(_actualStartTimestamp).TotalSeconds;
        var frame = Math.Max(0, (long)(elapsedSeconds * SampleRate) + _frameBias);
        return new BrowserPlaybackAnchor(_instance, "sync-lab-song-r1", SampleRate, frame,
            Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow, "Playing", 1, _sequence, 1);
    }
}
