using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuotti.AudioEngine.Output;
using Nuotti.AudioEngine.Playback;
using System.Collections.Generic;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class BackendSelectionTests
{
    private (ServiceProvider provider, EngineOptions options) Build(string? backendValue)
    {
        var dict = new Dictionary<string, string?>();
        if (backendValue != null)
            dict[nameof(EngineOptions.OutputBackend)] = backendValue;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var services = new ServiceCollection();
        services.AddAudioBackends(config);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EngineOptions>>().Value;
        return (provider, options);
    }

    [Fact]
    public void Resolves_PortAudio_When_Configured()
    {
        var (sp, _) = Build("PortAudio");
        var backend = sp.GetRequiredService<IAudioBackend>();
        Assert.IsType<PortAudioBackend>(backend);
        var player = backend.CreatePlayer(sp.GetRequiredService<IOptions<EngineOptions>>().Value);
        Assert.IsAssignableFrom<IAudioPlayer>(player);
    }

    [Fact]
    public void Defaults_To_SystemPlayer_When_Missing()
    {
        var (sp, _) = Build(null);
        var backend = sp.GetRequiredService<IAudioBackend>();
        Assert.IsType<SystemPlayerBackend>(backend);
    }

    [Fact]
    public void Falls_Back_To_SystemPlayer_When_Invalid()
    {
        var (sp, _) = Build("does-not-exist");
        var backend = sp.GetRequiredService<IAudioBackend>();
        Assert.IsType<SystemPlayerBackend>(backend);
    }
}