using Nuotti.Contracts.V1.Message;

namespace Nuotti.SimKit.Hub;

/// <summary>
/// Thrown when the Backend rejects a Command. Rejection is a return value on the server
/// (see SessionCommandProcessor); it becomes an exception here because a scenario that
/// issues an illegal Command has a bug in the scenario, and should stop loudly.
/// </summary>
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(CommandBase command, string responseBody)
        : base($"Command {command.GetType().Name} for session '{command.SessionCode}' was rejected: {responseBody}")
    {
        Command = command;
        ResponseBody = responseBody;
    }

    public CommandBase Command { get; }
    public string ResponseBody { get; }
}
