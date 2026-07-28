using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Maps a broadcast payload type to the SignalR method name the Backend sends it under.
/// </summary>
/// <remarks>
/// Mirrors Nuotti.Backend.Eventing.Subscribers.HubBroadcastSubscriber, which owns the outbound
/// wire contract. Two entries are not derivable from the type name and must not be guessed:
/// StopTrack is sent as "Stop", and GameStateChanged sends the bare GameStateSnapshot rather
/// than the event envelope — so the snapshot, not the event, is the payload type here.
///
/// If a broadcast is added to HubBroadcastSubscriber, add it here too.
/// </remarks>
public static class HubWireNames
{
    public static IReadOnlyDictionary<Type, string> ByPayloadType { get; } = new Dictionary<Type, string>
    {
        [typeof(GameStateSnapshot)] = "GameStateChanged",
        [typeof(AnswerSubmitted)] = "AnswerSubmitted",
        [typeof(QuestionPushed)] = "QuestionPushed",
        [typeof(PlayTrack)] = "PlayTrack",
        [typeof(StopTrack)] = "Stop",
    };

    public static string For<T>() =>
        ByPayloadType.TryGetValue(typeof(T), out var name)
            ? name
            : throw new NotSupportedException(
                $"{typeof(T).Name} is not a broadcast payload. Subscribable payloads are: " +
                string.Join(", ", ByPayloadType.Keys.Select(t => t.Name)));
}
