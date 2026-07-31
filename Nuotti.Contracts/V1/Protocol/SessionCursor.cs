namespace Nuotti.Contracts.V1.Protocol;

/// <summary>The last durable Event observed for one Workspace-scoped Session.</summary>
public sealed record SessionCursor(string WorkspaceId, string SessionCode, SessionSequence Sequence)
{
    public SessionCursor AdvanceTo(SessionSequence sequence)
    {
        if (sequence.Value <= Sequence.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A Session cursor must advance monotonically.");
        }

        return this with { Sequence = sequence };
    }
}
