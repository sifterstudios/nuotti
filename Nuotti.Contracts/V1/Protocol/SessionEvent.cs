using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>A uniquely identified Event at one durable, Session-local sequence.</summary>
public sealed record SessionEvent<TEvent>(
    SessionProtocolVersion Version,
    string WorkspaceId,
    SessionSequence Sequence,
    TEvent Event)
    where TEvent : EventBase
{
    [JsonIgnore]
    public Guid EventId => Event.EventId;

    [JsonIgnore]
    public SessionCursor Cursor => new(WorkspaceId, Event.SessionCode, Sequence);
}
