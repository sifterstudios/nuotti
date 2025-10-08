using Xunit;
using static Nuotti.SimKit.Program;

namespace Nuotti.SimKit.Tests;

public class CliArgParsingTests
{
    [Theory]
    [InlineData("baseline", "baseline")]
    [InlineData("load", "load")]
    [InlineData("chaos", "chaos")]
    [InlineData("BASELINE", "baseline")]
    public void Preset_values_are_parsed(string preset, string expectedLower)
    {
        var args = new[] { "run", "--backend", "http://x", "--session", "dev", "--preset", preset };
        var ok = TryParseRunArgs(args, out var parsed, out var error);
        Assert.True(ok, error);
        Assert.NotNull(parsed);
        Assert.Equal(expectedLower, parsed!.Preset.ToString().ToLowerInvariant());
    }

    [Fact]
    public void Missing_required_fails()
    {
        var args = new[] { "run", "--backend", "http://x" };
        var ok = TryParseRunArgs(args, out var parsed, out var error);
        Assert.False(ok);
        Assert.Null(parsed);
        Assert.Contains("--backend", error);
    }

    [Fact]
    public void Overrides_are_parsed()
    {
        var args = new[]
        {
            "run",
            "--backend","http://x",
            "--session","dev",
            "--preset","load",
            "--audiences","250",
            "--jitter","12.5",
            "--disconnect-rate","0.15",
            "--speed","2",
        };
        var ok = TryParseRunArgs(args, out var parsed, out var error);
        Assert.True(ok, error);
        Assert.NotNull(parsed);
        Assert.Equal(Preset.Load, parsed!.Preset);
        Assert.Equal(250, parsed.Audiences);
        Assert.Equal(12.5, parsed.JitterMs!.Value, 3);
        Assert.Equal(0.15, parsed.DisconnectRate!.Value, 3);
        Assert.Equal(2.0, parsed.Speed);
        Assert.False(parsed.Instant);
    }

    [Theory]
    [InlineData("--audiences","-1")]
    [InlineData("--jitter","-0.1")]
    [InlineData("--disconnect-rate","1.1")]
    [InlineData("--disconnect-rate","-0.1")]
    [InlineData("--preset","unknown")]
    public void Invalid_values_fail(string name, string value)
    {
        var args = new[] { "run", "--backend", "http://x", "--session", "dev", name, value };
        var ok = TryParseRunArgs(args, out var parsed, out var error);
        Assert.False(ok);
        Assert.Null(parsed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Instant_flag_overrides_speed_in_effective_use_but_parsing_keeps_both()
    {
        var args = new[] { "run", "--backend", "http://x", "--session", "dev", "--speed","3", "--instant" };
        var ok = TryParseRunArgs(args, out var parsed, out var error);
        Assert.True(ok, error);
        Assert.NotNull(parsed);
        Assert.Equal(3.0, parsed!.Speed);
        Assert.True(parsed.Instant);
    }
}
