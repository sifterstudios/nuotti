using System.Reflection;
using System.Runtime.InteropServices;

namespace ServiceDefaults;

/// <summary>
/// Helper to extract version and build information from assembly metadata.
/// </summary>
public static class VersionInfo
{
    /// <summary>
    /// Gets version information for the current application.
    /// </summary>
    public static VersionInfoResult GetVersionInfo(string serviceName)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        // Try to extract git commit from AssemblyMetadata or version string
        var gitCommit = ExtractGitCommit(informationalVersion, assembly);
        
        // Try to extract build time from AssemblyMetadata
        var buildTime = ExtractBuildTime(assembly);

        return new VersionInfoResult(
            Service: serviceName,
            Version: informationalVersion,
            GitCommit: gitCommit,
            BuildTime: buildTime,
            Runtime: RuntimeInformation.FrameworkDescription
        );
    }

    private static string? ExtractGitCommit(string informationalVersion, Assembly assembly)
    {
        // Check AssemblyMetadata for GitCommit
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        foreach (var attr in metadata)
        {
            if (string.Equals(attr.Key, "GitCommit", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(attr.Key, "GitSha", StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }
        }

        // Try to extract from informational version (e.g., "1.0.0+abc123")
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex >= 0 && plusIndex < informationalVersion.Length - 1)
        {
            return informationalVersion.Substring(plusIndex + 1);
        }

        return null;
    }

    private static string? ExtractBuildTime(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        foreach (var attr in metadata)
        {
            if (string.Equals(attr.Key, "BuildTime", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attr.Key, "BuildTimestamp", StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }
        }
        return null;
    }
}

/// <summary>
/// Version information result.
/// </summary>
public sealed record VersionInfoResult(
    string Service,
    string Version,
    string? GitCommit,
    string? BuildTime,
    string Runtime
);

