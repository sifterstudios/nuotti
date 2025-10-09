using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Performer.Services;

public sealed class CommandHistoryService
{
    public const int MaxEntries = 50;

    private readonly LinkedList<CommandHistoryEntry> _entries = new();
    private readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<CommandHistoryEntry> GetEntries()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }

    public void RecordSuccess(CommandBase cmd)
    {
        var entry = CreateEntry(cmd, CommandResult.Ok, null);
        Add(entry);
    }

    public void RecordFailure(CommandBase cmd, NuottiProblem? problem)
    {
        var entry = CreateEntry(cmd, CommandResult.Error, problem);
        Add(entry);
    }

    private CommandHistoryEntry CreateEntry(CommandBase cmd, CommandResult result, NuottiProblem? problem)
    {
        var payload = JsonSerializer.Serialize(cmd, cmd.GetType(), ContractsJson.RestOptions);
        return new CommandHistoryEntry
        {
            CommandName = cmd.GetType().Name,
            CommandId = cmd.CommandId,
            CorrelationId = cmd.CommandId,
            Timestamp = DateTimeOffset.Now,
            PayloadJson = payload,
            Result = result,
            Problem = problem
        };
    }

    private void Add(CommandHistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }
        Changed?.Invoke();
    }
}

public enum CommandResult
{
    Ok,
    Error
}

public sealed class CommandHistoryEntry
{
    public required string CommandName { get; init; }
    public required Guid CommandId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string PayloadJson { get; init; }
    public required CommandResult Result { get; init; }
    public NuottiProblem? Problem { get; init; }

    // Collected event ids that were correlated to this command (CausedByCommandId == CommandId)
    public List<Guid> CorrelatedEventIds { get; } = new();
}