using FluentAssertions;
using System.Text.Json;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class MetricsTests
{
    [Fact]
    public void Metrics_JSON_shape_is_stable()
    {
        var m = new AudioEngineMetrics();
        var json = m.ToJson();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("playing", out var p1).Should().BeTrue();
        p1.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        root.TryGetProperty("currentFile", out var p2).Should().BeTrue();
        (p2.ValueKind == JsonValueKind.String || p2.ValueKind == JsonValueKind.Null).Should().BeTrue();
        root.TryGetProperty("uptimeSeconds", out var p3).Should().BeTrue();
        p3.ValueKind.Should().Be(JsonValueKind.Number);
        root.TryGetProperty("lastError", out var p4).Should().BeTrue();
        (p4.ValueKind == JsonValueKind.String || p4.ValueKind == JsonValueKind.Null).Should().BeTrue();
        root.TryGetProperty("averageRttMs", out var p5).Should().BeTrue();
        (p5.ValueKind == JsonValueKind.Number || p5.ValueKind == JsonValueKind.Null).Should().BeTrue();
    }

    [Fact]
    public void Metrics_values_update_during_playback_simulation()
    {
        var m = new AudioEngineMetrics();
        // Initially not playing
        var s0 = m.Snapshot();
        s0.Playing.Should().BeFalse();
        s0.CurrentFile.Should().BeNull();

        // Start playing
        m.SetPlaying("file://abc.wav");
        var s1 = m.Snapshot();
        s1.Playing.Should().BeTrue();
        s1.CurrentFile.Should().Be("file://abc.wav");

        // RTT sample
        m.AddRttSample(10);
        var s2 = m.Snapshot();
        s2.AverageRttMs.Should().Be(10);

        // Stop
        m.SetStopped();
        var s3 = m.Snapshot();
        s3.Playing.Should().BeFalse();
    }
}
