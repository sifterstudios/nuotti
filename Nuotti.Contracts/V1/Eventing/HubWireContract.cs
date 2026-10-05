using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.V1.Eventing;

/// <summary>
/// The Fan-out wire contract: payload type → SignalR method name, plus unwrap rules.
/// Backend HubBroadcastSubscriber and SimKit HubWireNames both read from here so they cannot drift.
/// </summary>
public static class HubWireContract
{
    /// <summary>
    /// Session-group broadcasts keyed by the payload type a client subscribes with.
    /// <see cref="GameStateChanged"/> unwraps to a bare <see cref="GameStateSnapshot"/>.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> SessionBroadcasts { get; } =
        new Dictionary<Type, string>
        {
            [typeof(GameStateSnapshot)] = "GameStateChanged",
            [typeof(AnswerSubmitted)] = "AnswerSubmitted",
            [typeof(QuestionPushed)] = "QuestionPushed",
            [typeof(PlayTrack)] = "PlayTrack",
            [typeof(StopTrack)] = "Stop",
        };

    /// <summary>
    /// Show Agent command inbox names. Intentionally different from session SignalR
    /// (<c>StopTrack</c> stays <c>StopTrack</c>; <c>PreparePlayback</c> is <c>Prepare</c>).
    /// </summary>
    public static IReadOnlyDictionary<Type, string> ShowAgentCommands { get; } =
        new Dictionary<Type, string>
        {
            [typeof(PlayTrack)] = "PlayTrack",
            [typeof(StopTrack)] = "StopTrack",
            [typeof(PreparePlayback)] = "Prepare",
        };

    public static string SessionMethodFor(Type payloadType) =>
        SessionBroadcasts.TryGetValue(payloadType, out var name)
            ? name
            : throw new NotSupportedException(
                $"{payloadType.Name} is not a session broadcast payload. Subscribable payloads are: " +
                string.Join(", ", SessionBroadcasts.Keys.Select(t => t.Name)));

    public static string SessionMethodFor<T>() => SessionMethodFor(typeof(T));

    /// <summary>Maps a bus message to the SignalR method and wire payload for session Fan-out.</summary>
    public static bool TrySessionWire(object message, out string method, out object wirePayload)
    {
        switch (message)
        {
            case GameStateChanged changed:
                method = SessionBroadcasts[typeof(GameStateSnapshot)];
                wirePayload = changed.Snapshot;
                return true;
            case AnswerSubmitted:
            case QuestionPushed:
            case PlayTrack:
            case StopTrack:
                method = SessionMethodFor(message.GetType());
                wirePayload = message;
                return true;
            default:
                method = string.Empty;
                wirePayload = message;
                return false;
        }
    }

    public static bool TryShowAgentCommand(object message, out string messageType)
    {
        if (ShowAgentCommands.TryGetValue(message.GetType(), out messageType!))
            return true;
        messageType = string.Empty;
        return false;
    }
}
