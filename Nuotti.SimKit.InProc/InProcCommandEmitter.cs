using Nuotti.Backend.Commands;
using Nuotti.Contracts.V1.Message;
using Nuotti.SimKit.Actors;
using Nuotti.SimKit.Hub;

namespace Nuotti.SimKit.InProc;

/// <summary>
/// Applies Commands straight to a <see cref="ISessionCommandProcessor"/>, with no transport.
/// </summary>
/// <remarks>
/// The in-memory counterpart to HttpCommandEmitter, and one half of the fidelity swap: a
/// scenario is written once and run either through this or over HTTP without changing.
/// </remarks>
public sealed class InProcCommandEmitter(ISessionCommandProcessor processor, Actor actor) : ICommandEmitter
{
    public async Task EmitAsync(CommandBase command, CancellationToken cancellationToken = default)
    {
        var result = await processor
            .ApplyAsync(command.SessionCode, actor, command, correlationId: null, cancellationToken)
            .ConfigureAwait(false);

        // Duplicate is an idempotency hit, not a failure — the intent is satisfied. Only a
        // genuine rejection means the scenario asked for something illegal.
        if (result.Outcome == Outcome.Rejected)
            throw new CommandRejectedException(command, result.Problem?.Detail ?? "rejected");
    }
}
