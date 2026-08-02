namespace Nuotti.Backend.Realtime;

/// <summary>
/// The only place SignalR group names are built.
/// </summary>
/// <remarks>
/// Session codes are matched case-insensitively everywhere they are authenticated: a phone that
/// types "abc123" for session "ABC123" joins successfully. Group names are plain strings and are
/// matched exactly, so the two spellings used to become two different groups - the phone joined,
/// was told nothing was wrong, and then received nothing all night. Normalising in one place is
/// what keeps the subscriber and the hub talking about the same room.
/// </remarks>
public static class RealtimeGroups
{
    public static string Session(string sessionCode) => Normalize(sessionCode);

    public static string SessionRole(string sessionCode, string role)
        => $"{Normalize(sessionCode)}:{role.Trim().ToLowerInvariant()}";

    public static string Workspace(string workspaceId, string sessionCode)
        => $"{workspaceId}:{Normalize(sessionCode)}";

    static string Normalize(string sessionCode) => sessionCode.Trim().ToUpperInvariant();
}
