using System.Text.Json.Serialization;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>
/// A durable Session state checkpoint. Replay resumes strictly after LastSequence.
/// MinimumReaderVersion lets producers add fields without requiring every reader to understand
/// the writer's complete minor revision.
/// </summary>
public sealed record SessionSnapshot<TState>(
    SessionProtocolVersion WriterVersion,
    SessionProtocolVersion MinimumReaderVersion,
    string WorkspaceId,
    string SessionCode,
    SessionSequence LastSequence,
    ControlGeneration ControlGeneration,
    TState State)
{
    [JsonIgnore]
    public SessionCursor Cursor => new(WorkspaceId, SessionCode, LastSequence);

    public bool CanBeReadBy(SessionProtocolVersion readerVersion)
        => readerVersion.Major == WriterVersion.Major
           && readerVersion.IsAtLeast(MinimumReaderVersion);
}
