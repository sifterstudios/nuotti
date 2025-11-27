using System.Diagnostics;

namespace Nuotti.Backend.Telemetry;

/// <summary>
/// ActivitySource for Backend service OpenTelemetry tracing.
/// Provides spans around command handling and event broadcasting.
/// </summary>
public static class BackendActivitySource
{
    private static readonly ActivitySource _source = new("Nuotti.Backend");

    public static ActivitySource Source => _source;

    /// <summary>
    /// Creates a span for command handling.
    /// </summary>
    public static Activity? StartCommandHandling(string commandName, string session, Guid commandId)
    {
        var activity = _source.StartActivity($"command.{commandName}");
        if (activity != null)
        {
            activity.SetTag("command.name", commandName);
            activity.SetTag("command.id", commandId.ToString());
            activity.SetTag("session.code", session);
            activity.SetTag("nuotti.command_type", "phase_change");
        }
        return activity;
    }

    /// <summary>
    /// Creates a span for event broadcasting.
    /// </summary>
    public static Activity? StartEventBroadcast(string eventType, string session, Guid? correlationId = null)
    {
        var activity = _source.StartActivity($"event.broadcast.{eventType}");
        if (activity != null)
        {
            activity.SetTag("event.type", eventType);
            activity.SetTag("session.code", session);
            if (correlationId.HasValue)
            {
                activity.SetTag("correlation.id", correlationId.Value.ToString());
            }
            activity.SetTag("nuotti.event_type", eventType);
        }
        return activity;
    }

    /// <summary>
    /// Creates a span for event processing by a subscriber.
    /// </summary>
    public static Activity? StartEventProcessing(string eventType, string subscriberName, string session)
    {
        var activity = _source.StartActivity($"event.process.{subscriberName}.{eventType}");
        if (activity != null)
        {
            activity.SetTag("event.type", eventType);
            activity.SetTag("subscriber.name", subscriberName);
            activity.SetTag("session.code", session);
        }
        return activity;
    }
}

