using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Nuotti.AudioEngine.Output;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAudioBackends(this IServiceCollection services, IConfiguration configuration)
    {
        // Manually bind EngineOptions and expose via IOptions<EngineOptions>
        services.AddSingleton<IOptions<EngineOptions>>(_ =>
        {
            var opts = new EngineOptions();
            configuration.Bind(opts);
            return Options.Create(opts);
        });

        services.AddSingleton<SystemPlayerBackend>();
        services.AddSingleton<PortAudioBackend>();

        services.AddSingleton<IAudioBackend>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<EngineOptions>>().Value;
            var backend = opts.OutputBackend?.Trim();
            if (string.IsNullOrEmpty(backend))
            {
                return sp.GetRequiredService<SystemPlayerBackend>();
            }

            switch (backend.ToLowerInvariant())
            {
                case "system":
                case "systemplayer":
                case "default":
                    return sp.GetRequiredService<SystemPlayerBackend>();
                case "portaudio":
                    return sp.GetRequiredService<PortAudioBackend>();
                default:
                    // Fallback
                    return sp.GetRequiredService<SystemPlayerBackend>();
            }
        });

        return services;
    }
}