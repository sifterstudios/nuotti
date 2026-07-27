using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
namespace Nuotti.Backend.Eventing.Subscribers;

/// <summary>
/// Writes the dev log-stream line for relay commands. These lines were four copies of the same
/// boilerplate in ApiEndpoints before the fan-out owned them.
/// </summary>
public sealed class LogStreamSubscriber : IDisposable
{
    readonly List<IDisposable> _subs = [];
    readonly ILogStreamer _log;

    public LogStreamSubscriber(IEventBus bus, ILogStreamer log)
    {
        _log = log;

        _subs.Add(bus.Subscribe<QuestionPushed>((cmd, _) =>
            Write(cmd.SessionCode, $"QuestionPushed to session={cmd.SessionCode}: {cmd.Text}")));
        _subs.Add(bus.Subscribe<PlayTrack>((cmd, _) =>
            Write(cmd.SessionCode, $"Play requested for session={cmd.SessionCode}: url={cmd.FileUrl}")));
        _subs.Add(bus.Subscribe<StopTrack>((cmd, _) =>
            Write(cmd.SessionCode, $"Stop requested for session={cmd.SessionCode}")));
    }

    Task Write(string session, string message)
        => _log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: "Program",
            Message: message,
            Session: session));

    public void Dispose()
    {
        foreach (var sub in _subs) sub.Dispose();
        _subs.Clear();
    }
}
