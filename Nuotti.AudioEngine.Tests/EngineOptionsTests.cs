using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class EngineOptionsTests
{
    [Fact]
    public void Binds_and_validates_from_json()
    {
        // Arrange: engine.json content
        var json = """
        {
          "PreferredPlayer": "Vlc",
          "OutputBackend": "Wasapi",
          "OutputDevice": "Speakers (Realtek)",
          "Routes": {
            "Tracks": "bus1",
            "Click": "bus2"
          },
          "LogLevel": "Debug"
        }
        """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var cfg = new ConfigurationBuilder()
            .AddJsonStream(ms)
            .Build();

        var opts = new EngineOptions();
        cfg.Bind(opts);

        // Act + Assert Validate: should not throw
        var act = () => opts.Validate();
        act.Should().NotThrow();

        // Verify values
        opts.PreferredPlayer.Should().Be(PreferredPlayer.Vlc);
        opts.OutputBackend.Should().Be("Wasapi");
        opts.OutputDevice.Should().Be("Speakers (Realtek)");
        opts.Routes.Tracks.Should().Be("bus1");
        opts.Routes.Click.Should().Be("bus2");
        opts.LogLevel.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public void Env_override_wins_over_json()
    {
        // Arrange
        var json = """
        {
          "PreferredPlayer": "Vlc",
          "Routes": { "Tracks": "jsonVal", "Click": "clickJson" },
          "LogLevel": "Information"
        }
        """;

        // Set env var override
        var envKey1 = "NUOTTI_ENGINE__PreferredPlayer";
        var envKey2 = "NUOTTI_ENGINE__Routes__Tracks";
        var envKey3 = "NUOTTI_ENGINE__LogLevel";
        Environment.SetEnvironmentVariable(envKey1, "Ffplay");
        Environment.SetEnvironmentVariable(envKey2, "envVal");
        Environment.SetEnvironmentVariable(envKey3, "Warning");
        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var cfg = new ConfigurationBuilder()
                .AddJsonStream(ms)
                .AddEnvironmentVariables(prefix: "NUOTTI_ENGINE__")
                .Build();

            var opts = new EngineOptions();
            cfg.Bind(opts);
            opts.Validate();

            // Assert overrides applied
            opts.PreferredPlayer.Should().Be(PreferredPlayer.Ffplay);
            opts.Routes.Tracks.Should().Be("envVal");
            opts.LogLevel.Should().Be(LogLevel.Warning);
            // Non-overridden values fall back to JSON
            opts.Routes.Click.Should().Be("clickJson");
        }
        finally
        {
            // Cleanup env vars
            Environment.SetEnvironmentVariable(envKey1, null);
            Environment.SetEnvironmentVariable(envKey2, null);
            Environment.SetEnvironmentVariable(envKey3, null);
        }
    }

    [Fact]
    public void Validate_throws_when_routes_missing()
    {
        var opts = new EngineOptions
        {
            Routes = new RoutesOptions { Tracks = "", Click = "" }
        };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }
}
