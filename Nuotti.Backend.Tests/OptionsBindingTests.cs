using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Models;

namespace Nuotti.Backend.Tests;

public class OptionsBindingTests
{
    [Fact]
    public void Binds_FromJson_Section_ToStronglyTypedOptions()
    {
        var json = """
        {
          "ServiceName": "Nuotti.Backend",
          "Greeting": "Hello from Tests"
        }
        """;

        using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var config = new ConfigurationBuilder()
            .AddJsonStream(jsonStream)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<NuottiOptions>().Bind(config);

        using var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<NuottiOptions>>().Value;

        Assert.Equal("Nuotti.Backend", opts.ServiceName);
        Assert.Equal("Hello from Tests", opts.Greeting);
    }

    [Fact]
    public void Env_Vars_WithPrefix_Override_Config()
    {
        // Set environment variable to override Greeting BEFORE building configuration
        var envVarName = "NUOTTI_GREETING"; // maps to Greeting (root)
        try
        {
            Environment.SetEnvironmentVariable(envVarName, "Hello from Env");

            var baseConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ServiceName"] = "Nuotti.Backend",
                    ["Greeting"] = "Hello from Base"
                })
                // Add env variables provider with NUOTTI_ prefix
                .AddEnvironmentVariables(prefix: "NUOTTI_")
                .Build();

            // Sanity-check the configuration has the expected values (env should override)
            Assert.Equal("Nuotti.Backend", baseConfig["ServiceName"]);
            Assert.Equal("Hello from Env", baseConfig["Greeting"]);

            var services = new ServiceCollection();
            services.AddOptions<NuottiOptions>()
                .Bind(baseConfig);

            using var provider = services.BuildServiceProvider();
            var opts = provider.GetRequiredService<IOptions<NuottiOptions>>().Value;

            Assert.Equal("Nuotti.Backend", opts.ServiceName);
            Assert.Equal("Hello from Env", opts.Greeting);
        }
        finally
        {
            // Clean up to avoid leaking env var to other tests
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }
}
