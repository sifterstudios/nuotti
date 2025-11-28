using Microsoft.Extensions.Configuration;

namespace ServiceDefaults;

/// <summary>
/// Helper class for accessing feature flags from configuration.
/// Feature flags are defined in the "Features:*" configuration section.
/// Example: Features:ExperimentalFeature: true
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// Checks if a feature flag is enabled.
    /// </summary>
    /// <param name="configuration">Configuration root to read from.</param>
    /// <param name="featureName">Name of the feature flag.</param>
    /// <param name="defaultValue">Default value if flag is not set (default: false).</param>
    /// <returns>True if the feature is enabled, false otherwise.</returns>
    public static bool IsEnabled(IConfiguration configuration, string featureName, bool defaultValue = false)
    {
        var key = $"Features:{featureName}";
        return configuration.GetValue<bool>(key, defaultValue);
    }

    /// <summary>
    /// Gets all feature flags as a dictionary.
    /// </summary>
    /// <param name="configuration">Configuration root to read from.</param>
    /// <returns>Dictionary of feature name to enabled state.</returns>
    public static Dictionary<string, bool> GetAll(IConfiguration configuration)
    {
        var features = new Dictionary<string, bool>();
        var featuresSection = configuration.GetSection("Features");
        
        foreach (var child in featuresSection.GetChildren())
        {
            var enabled = child.Get<bool>();
            features[child.Key] = enabled;
        }
        
        return features;
    }
}

