using Nuotti.Contracts.V1.Model;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>
/// Explicit acknowledgement of a Command. Applied and Duplicate may identify the resulting
/// cursor; Rejected carries a problem and never implies a mutation.
/// </summary>
public sealed record SessionCommandResult(
    SessionProtocolVersion Version,
    Guid CommandId,
    Outcome Outcome,
    SessionCursor? Cursor,
    NuottiProblem? Problem);
