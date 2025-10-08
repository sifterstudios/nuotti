using Nuotti.SimKit.Metrics;
using System.Text.Json;
using Xunit;
namespace Nuotti.SimKit.Tests;

public class RunSummaryTests
{
    [Fact]
    public void Report_files_created_and_json_schema_validates()
    {
        // Arrange: build a collector with deterministic times
        var t0 = new DateTimeOffset(2025, 10, 8, 19, 50, 0, TimeSpan.Zero);
        var collector = new RunMetricsCollector(t0);

        // simulate some commands and answers
        collector.RecordCommandIssued("c1");
        Advance(50);
        collector.RecordCommandApplied("c1");

        collector.RecordCommandIssued("c2");
        Advance(120);
        collector.RecordCommandApplied("c2");

        collector.RecordAnswerSubmitted();
        collector.RecordAnswerSubmitted();
        collector.RecordDisconnection();
        collector.RecordError();

        var t1 = t0.AddSeconds(10);
        var metrics = collector.BuildSnapshot(t1);

        // Act: write the summary under a temporary base dir
        string baseDir = Path.Combine(Path.GetTempPath(), "NuottiSimKitTests", Guid.NewGuid().ToString("N"));
        var (_, jsonPath, mdPath) = RunSummaryWriter.Write(baseDir, metrics, nowUtc: t1);

        // Assert: folder/file existence and naming pattern
        Assert.True(File.Exists(jsonPath), "report.json should exist");
        Assert.True(File.Exists(mdPath), "report.md should exist");

        var dir = Path.GetDirectoryName(jsonPath)!;
        var parent = Directory.GetParent(dir)!.FullName;
        Assert.Equal("runs", Path.GetFileName(parent));
        var folderName = Path.GetFileName(dir);
        Assert.Matches(@"^\d{8}-\d{4}$", folderName);

        // Validate JSON against minimal schema
        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        Assert.True(ValidateAgainstSchema(doc.RootElement, out var error), error);

        // Cleanup temp
        try { Directory.Delete(baseDir, recursive: true); } catch { /* ignore */ }

        // Local function to simulate time passing in-place (no global clock used; just spacing method calls)
        static void Advance(int ms) => Thread.Sleep(1); // do nothing substantial
    }

    private static bool ValidateAgainstSchema(JsonElement root, out string error)
    {
        string[] requiredProps = new[]
        {
            "startedAtUtc","endedAtUtc",
            "commandApplyLatencyP50Ms","commandApplyLatencyP95Ms",
            "disconnections","errors","answerThroughputPerSec",
            "commandsIssued","commandsApplied","answersSubmitted"
        };
        foreach (var p in requiredProps)
        {
            if (!root.TryGetProperty(p, out var _))
            {
                error = $"Missing required property: {p}";
                return false;
            }
        }
        // type checks
        if (root.GetProperty("startedAtUtc").ValueKind != JsonValueKind.String)
        { error = "startedAtUtc must be string (ISO date)"; return false; }
        if (root.GetProperty("endedAtUtc").ValueKind != JsonValueKind.String)
        { error = "endedAtUtc must be string (ISO date)"; return false; }

        if (!IsNumber(root, "commandApplyLatencyP50Ms", out error)) return false;
        if (!IsNumber(root, "commandApplyLatencyP95Ms", out error)) return false;
        if (!IsInteger(root, "disconnections", out error)) return false;
        if (!IsInteger(root, "errors", out error)) return false;
        if (!IsNumber(root, "answerThroughputPerSec", out error)) return false;
        if (!IsInteger(root, "commandsIssued", out error)) return false;
        if (!IsInteger(root, "commandsApplied", out error)) return false;
        if (!IsInteger(root, "answersSubmitted", out error)) return false;

        error = string.Empty;
        return true;

        static bool IsNumber(JsonElement root, string name, out string error)
        {
            if (root.GetProperty(name).ValueKind is JsonValueKind.Number)
            { error = string.Empty; return true; }
            error = $"{name} must be number"; return false;
        }
        static bool IsInteger(JsonElement root, string name, out string error)
        {
            var el = root.GetProperty(name);
            if (el.ValueKind is JsonValueKind.Number && el.TryGetInt32(out _))
            { error = string.Empty; return true; }
            error = $"{name} must be integer"; return false;
        }
    }
}