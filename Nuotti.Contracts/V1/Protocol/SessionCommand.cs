using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>
/// A versioned, Workspace-scoped Command intent. The embedded CommandId is the idempotency key.
/// ExpectedControlGeneration rejects a stale controller before it mutates the Session.
/// </summary>
public sealed record SessionCommand<TCommand>(
    SessionProtocolVersion Version,
    string WorkspaceId,
    TCommand Command,
    ControlGeneration ExpectedControlGeneration)
    where TCommand : CommandBase
{
    [JsonIgnore]
    public Guid CommandId => Command.CommandId;

    [JsonIgnore]
    public string SessionCode => Command.SessionCode;
}
