using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Thrown when the Backend rejects a Command. Rejection is a return value on the server
/// (see SessionCommandProcessor); it becomes an exception here because a scenario that
/// issues an illegal Command has a bug in the scenario, and should stop loudly.
/// </summary>
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(CommandBase command, string rawPayload)
        : base($"Command {command.GetType().Name} for session '{command.SessionCode}' was rejected: {rawPayload}")
    {
        Command = command;
        RawPayload = rawPayload;
    }

    public CommandBase Command { get; }

    /// <summary>
    /// The rejection payload as delivered at this fidelity: the full HTTP response body over
    /// HTTP, or the processor's rejection detail in-proc — the two are not the same shape, which
    /// is exactly why <see cref="Problem"/> exists for anything that needs to reason about *why*
    /// a command was rejected. Named RawPayload rather than ResponseBody because there is no HTTP
    /// response in-proc.
    /// </summary>
    public string RawPayload { get; }

    /// <summary>
    /// The structured rejection reason, when the emitter could recover one. Both
    /// HttpCommandEmitter and InProcCommandEmitter populate this from the same
    /// <see cref="NuottiProblem"/> the Backend's SessionCommandProcessor produced, so a caller can
    /// ask "was this rejected for UnauthorizedRole?" identically at both fidelities instead of
    /// pattern-matching <see cref="RawPayload"/>.
    /// </summary>
    public NuottiProblem? Problem { get; init; }
}
