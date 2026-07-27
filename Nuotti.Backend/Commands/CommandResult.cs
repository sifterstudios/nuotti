using Nuotti.Contracts.V1.Model;
namespace Nuotti.Backend.Commands;

/// <summary>
/// What happened to a Command.
/// </summary>
public enum Outcome
{
    /// <summary>The Command was accepted and its effects applied.</summary>
    Applied,

    /// <summary>The Command's id had already been seen within the idempotency window; nothing happened.</summary>
    Duplicate,

    /// <summary>The Command was refused. <see cref="CommandResult.Problem"/> says why.</summary>
    Rejected
}

/// <summary>
/// The result of applying a Command. Rejection is a return value, never an exception: a wrong role
/// or an illegal phase is an expected answer, not a fault.
/// </summary>
/// <param name="Outcome">Whether the Command was applied, de-duplicated, or refused.</param>
/// <param name="State">The resulting snapshot when the Command changed state; otherwise null.</param>
/// <param name="Problem">Set when and only when <paramref name="Outcome"/> is Rejected.</param>
public sealed record CommandResult(Outcome Outcome, GameStateSnapshot? State, NuottiProblem? Problem)
{
    public static CommandResult Applied(GameStateSnapshot? state = null)
        => new(Outcome.Applied, state, null);

    public static CommandResult Duplicate()
        => new(Outcome.Duplicate, null, null);

    public static CommandResult Rejected(NuottiProblem problem)
        => new(Outcome.Rejected, null, problem);
}
