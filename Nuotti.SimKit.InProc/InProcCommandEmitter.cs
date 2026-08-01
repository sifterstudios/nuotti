using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Protocol;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// Applies Commands straight to a <see cref="ISessionCommandProcessor"/>, with no transport.
/// </summary>
/// <remarks>
/// The in-memory counterpart to HttpCommandEmitter, and one half of the fidelity swap: a
/// scenario is written once and run either through this or over HTTP without changing.
///
/// Deliberately has no injected <see cref="Actor"/>: <see cref="Actor.Claimed"/> is derived from
/// each command's own IssuedByRole/IssuedById, exactly as <c>PhaseEndpoints.MapPhaseCommand</c>
/// does on the HTTP path. A constructor-supplied Actor would let this emitter simulate a
/// server-verified actor that HTTP can never produce (HTTP always claims from the body), so the
/// same scenario could be rejected in-proc and applied over HTTP for the same command — that
/// verification is the hub's job, not this emitter's.
/// </remarks>
public sealed class InProcCommandEmitter(ISessionCommandProcessor processor) : ICommandEmitter
{
    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        if (!HttpCommandEmitter.IsPhaseCommand(command))
        {
            throw new NotSupportedException(
                $"{command.GetType().Name} has no phase endpoint. Commands that are not phase " +
                "commands (for example SubmitAnswer) go through the hub, not this emitter.");
        }

        var actor = Actor.Claimed(command);
        var result = await processor
            .ApplyAsync(command.SessionCode, actor, command, correlationId: null, cancellationToken)
            .ConfigureAwait(false);

        // Duplicate is an idempotency hit, not a failure — the intent is satisfied. Only a
        // genuine rejection means the scenario asked for something illegal.
        if (result.Outcome == Outcome.Rejected)
        {
            throw new CommandRejectedException(command, result.Problem?.Detail ?? "rejected")
            {
                Problem = result.Problem
            };
        }
    }
}
