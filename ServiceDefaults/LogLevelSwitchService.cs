using Serilog.Core;
using Serilog.Events;

namespace ServiceDefaults;

/// <summary>
/// Service providing runtime log level switching capability.
/// Uses Serilog's LoggingLevelSwitch to allow dynamic log level changes.
/// </summary>
public sealed class LogLevelSwitchService
{
    private readonly LoggingLevelSwitch _levelSwitch;

    public LogLevelSwitchService(LogEventLevel initialLevel = LogEventLevel.Information)
    {
        _levelSwitch = new LoggingLevelSwitch(initialLevel);
    }

    /// <summary>
    /// Gets the current minimum log level.
    /// </summary>
    public LogEventLevel CurrentLevel => _levelSwitch.MinimumLevel;

    /// <summary>
    /// Sets the minimum log level dynamically.
    /// </summary>
    public void SetLevel(LogEventLevel level)
    {
        _levelSwitch.MinimumLevel = level;
    }

    /// <summary>
    /// Gets the LoggingLevelSwitch for use in Serilog configuration.
    /// </summary>
    internal LoggingLevelSwitch LevelSwitch => _levelSwitch;
}

