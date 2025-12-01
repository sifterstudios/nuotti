using System.Linq;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Serilog;
using Serilog.Context;

namespace Nuotti.Backend.Audit;

/// <summary>
/// Service for creating structured audit log entries for commands and events.
/// Audit logs are written to a separate file sink with rolling retention.
/// </summary>
public class AuditLogService
{
    private readonly Serilog.ILogger _auditLogger;
    private readonly IGameStateStore _gameStateStore;

    public AuditLogService(IGameStateStore gameStateStore, Serilog.ILogger logger)
    {
        _gameStateStore = gameStateStore;
        _auditLogger = logger;
    }

    /// <summary>
    /// Logs a command being applied.
    /// </summary>
    public void LogCommandApplied<TCommand>(TCommand command, string? stateSummary = null)
        where TCommand : CommandBase
    {
        var stateSummaryFinal = stateSummary ?? GetStateSummary(command.SessionCode);
        
        using (LogContext.PushProperty("audit.type", "command_applied"))
        using (LogContext.PushProperty("audit.command_type", typeof(TCommand).Name))
        using (LogContext.PushProperty("audit.command_id", command.CommandId))
        using (LogContext.PushProperty("audit.session_code", command.SessionCode))
        using (LogContext.PushProperty("audit.issued_by_role", command.IssuedByRole.ToString()))
        using (LogContext.PushProperty("audit.issued_by_id", command.IssuedById))
        using (LogContext.PushProperty("audit.issued_at_utc", command.IssuedAtUtc))
        using (LogContext.PushProperty("audit.state_summary", stateSummaryFinal))
        {
            _auditLogger.Information(
                "Command applied: Type={CommandType}, CommandId={CommandId}, Session={SessionCode}, Role={Role}, IssuedById={IssuedById}, IssuedAt={IssuedAt}, StateSummary={StateSummary}",
                typeof(TCommand).Name,
                command.CommandId,
                command.SessionCode,
                command.IssuedByRole,
                command.IssuedById,
                command.IssuedAtUtc,
                stateSummaryFinal);
        }
    }

    /// <summary>
    /// Logs an event being published.
    /// </summary>
    public void LogEventPublished<TEvent>(TEvent evt)
        where TEvent : EventBase
    {
        var stateSummary = GetStateSummary(evt.SessionCode);
        
        using (LogContext.PushProperty("audit.type", "event_published"))
        using (LogContext.PushProperty("audit.event_type", typeof(TEvent).Name))
        using (LogContext.PushProperty("audit.event_id", evt.EventId))
        using (LogContext.PushProperty("audit.correlation_id", evt.CorrelationId))
        using (LogContext.PushProperty("audit.caused_by_command_id", evt.CausedByCommandId))
        using (LogContext.PushProperty("audit.session_code", evt.SessionCode))
        using (LogContext.PushProperty("audit.emitted_at_utc", evt.EmittedAtUtc))
        using (LogContext.PushProperty("audit.state_summary", stateSummary))
        {
            _auditLogger.Information(
                "Event published: Type={EventType}, EventId={EventId}, CorrelationId={CorrelationId}, CausedByCommandId={CausedByCommandId}, Session={SessionCode}, EmittedAt={EmittedAt}, StateSummary={StateSummary}",
                typeof(TEvent).Name,
                evt.EventId,
                evt.CorrelationId,
                evt.CausedByCommandId,
                evt.SessionCode,
                evt.EmittedAtUtc,
                stateSummary);
        }
    }

    private string GetStateSummary(string sessionCode)
    {
        if (!_gameStateStore.TryGet(sessionCode, out var snapshot))
        {
            return "Session not found";
        }

        return $"Phase={snapshot.Phase}, SongIndex={snapshot.SongIndex}, TotalAnswers={snapshot.Tallies.Sum()}, Players={snapshot.Scores.Count}";
    }
}

