using System.Text.Json;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Backend.Persistence;

/// <summary>Single registry for Session message serialization and runtime-type publication.</summary>
public static class SessionMessagePublisher
{
    public sealed record WorkspacePublication(string WorkspaceId, string SessionCode, object Payload);
    sealed record Entry(Type Type, bool Durable, Func<IEventBus, object, CancellationToken, Task> Publish);

    static Entry Of<T>(bool durable = true) => new(typeof(T), durable,
        (bus, message, ct) => bus.PublishAsync((T)message, ct));

    static readonly Entry[] Entries =
    [
        Of<GamePhaseChanged>(), Of<CorrectAnswerRevealed>(), Of<HintGiven>(),
        Of<CatalogUpdated>(), Of<QuestionOffered>(), Of<AnswerSubmitted>(), Of<GameStateChanged>(),
        Of<QuestionPushed>(false), Of<PlayTrack>(false), Of<StopTrack>(false)
    ];

    static readonly IReadOnlyDictionary<Type, Entry> ByType = Entries.ToDictionary(entry => entry.Type);
    static readonly IReadOnlyDictionary<string, Entry> ByName = Entries.ToDictionary(entry => entry.Type.Name, StringComparer.Ordinal);

    public static async Task PublishAsync(IEventBus bus, object message,
        CancellationToken cancellationToken = default, string? workspaceId = null)
    {
        if (!ByType.TryGetValue(message.GetType(), out var entry))
            throw new NotSupportedException($"Message type '{message.GetType().Name}' is not publishable.");
        await entry.Publish(bus, message, cancellationToken);
        if (workspaceId is not null && workspaceId != "legacy")
        {
            var sessionCode = message switch
            {
                EventBase evt => evt.SessionCode,
                CommandBase command => command.SessionCode,
                _ => throw new NotSupportedException("Workspace publication requires a Session message.")
            };
            await bus.PublishAsync(new WorkspacePublication(workspaceId, sessionCode, message), cancellationToken);
        }
    }

    public static (string Type, string Payload) SerializeDurable(object message)
    {
        if (!ByType.TryGetValue(message.GetType(), out var entry) || !entry.Durable)
            throw new NotSupportedException($"Message type '{message.GetType().Name}' is not durable.");
        return (entry.Type.Name, JsonSerializer.Serialize(message, entry.Type, ContractsJson.RestOptions));
    }

    public static object DeserializeDurable(string messageType, string payload)
    {
        if (!ByName.TryGetValue(messageType, out var entry) || !entry.Durable)
            throw new NotSupportedException($"Message type '{messageType}' is not durable.");
        return JsonSerializer.Deserialize(payload, entry.Type, ContractsJson.RestOptions)
            ?? throw new JsonException($"Durable message '{messageType}' deserialized to null.");
    }
}
