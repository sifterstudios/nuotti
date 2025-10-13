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
          "Routing": {
            "tracks": [1,2],
            "click": [3]
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
        opts.Routing.Tracks.Should().Equal(1, 2);
        opts.Routing.Click.Should().Equal(3);
        opts.LogLevel.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public void Env_override_wins_over_json_for_scalars()
    {
        // Arrange
        var json = """
        {
          "PreferredPlayer": "Vlc",
          "Routing": { "tracks": [1], "click": [2] },
          "LogLevel": "Information"
        }
        """;

        // Set env var override (only scalars to avoid array binding complexity)
        var envKey1 = "NUOTTI_ENGINE__PreferredPlayer";
        var envKey3 = "NUOTTI_ENGINE__LogLevel";
        Environment.SetEnvironmentVariable(envKey1, "Ffplay");
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
            opts.LogLevel.Should().Be(LogLevel.Warning);
            // Routing remains from JSON
            opts.Routing.Tracks.Should().Equal(1);
            opts.Routing.Click.Should().Equal(2);
        }
        finally
        {
            // Cleanup env vars
            Environment.SetEnvironmentVariable(envKey1, null);
            Environment.SetEnvironmentVariable(envKey3, null);
        }
    }

    [Fact]
    public void Validate_throws_when_routing_missing()
    {
        var opts = new EngineOptions
        {
            Routing = new RoutingOptions { Tracks = null!, Click = null! }
        };
        var act = () => opts.Validate();
        act.Should().Throw<ArgumentException>();
    }
}
