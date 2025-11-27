namespace Nuotti.Backend.Sessions;

public interface ISessionStore
{
    // Touch or add connection with role for a session (updates last-seen)
    void Touch(string session, string role, string connectionId, string? audienceName = null);

    // Remove a connection (on disconnect/eviction)
    void Remove(string connectionId);

    // Get counts per role for a session
    RoleCounts GetCounts(string session);

    // Get aggregate counts across all sessions
    RoleCounts GetAggregateCounts();

    // Clear all data related to a session (DEV/testing support)
    void Clear(string session);
}

public readonly record struct RoleCounts(int Performer, int Projector, int Engine, int Audiences);
