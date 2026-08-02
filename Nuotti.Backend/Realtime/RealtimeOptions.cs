namespace Nuotti.Backend.Realtime;

/// <summary>Configuration for the single realtime hub, bound from the "Nuotti:Realtime" section.</summary>
public sealed class RealtimeOptions
{
    /// <summary>
    /// Whether a connection with no recognised credential may still reach the hub.
    /// </summary>
    /// <remarks>
    /// This exists so the local loop and the test suite keep working while the four shipped
    /// clients are migrated onto credentials one at a time. It is deliberately a single explicit
    /// flag rather than an <c>IsDevelopment()</c> branch: the hub itself is now identical in every
    /// environment, and the one thing that differs is visible in configuration rather than hidden
    /// in code. Production leaves it false, which is what makes /hub safe to expose there at all.
    /// </remarks>
    public bool AllowUnauthenticatedConnections { get; set; }
}
