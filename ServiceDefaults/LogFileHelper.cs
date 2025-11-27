namespace ServiceDefaults;

/// <summary>
/// Helper to determine log file paths for services that need log persistence.
/// </summary>
public static class LogFileHelper
{
    /// <summary>
    /// Gets the log directory for the service.
    /// Uses AppData\Roaming\Nuotti\Logs on Windows, ~/Nuotti/Logs elsewhere.
    /// </summary>
    public static string GetLogDirectory(string serviceName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var logDir = Path.Combine(appData, "Nuotti", "Logs", serviceName);
            Directory.CreateDirectory(logDir);
            return logDir;
        }
        
        // Fallback: use home directory
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeLogDir = Path.Combine(home, "Nuotti", "Logs", serviceName);
        Directory.CreateDirectory(homeLogDir);
        return homeLogDir;
    }

    /// <summary>
    /// Gets the log file path for the service.
    /// Format: {logDir}/{serviceName}-{date}.log
    /// </summary>
    public static string GetLogFilePath(string serviceName)
    {
        var logDir = GetLogDirectory(serviceName);
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        return Path.Combine(logDir, $"{serviceName}-{dateStr}.log");
    }
}

