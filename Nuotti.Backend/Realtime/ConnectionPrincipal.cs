using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Realtime;

/// <summary>
/// What a realtime connection is allowed to do, independent of what it claims to be.
/// </summary>
/// <remarks>
/// QuizHub.Join used to take a role as a plain string argument and believe it, which is why the
/// hub could only ever be mapped in Development: any client could declare itself the Performer of
/// any session. Capabilities are derived from a credential instead, so the same hub is safe to
/// expose in both environments and the two stop diverging.
/// </remarks>
public enum Capability
{
    /// <summary>Receive session state and events. Every principal has this.</summary>
    Subscribe,

    /// <summary>Drive the game: phases, questions, hints, reveal. Workspace members only.</summary>
    IssueGameCommand,

    /// <summary>Answer the current question, as one participant. Audience only.</summary>
    SubmitAnswer,

    /// <summary>Ask the venue rig to play or stop audio.</summary>
    RequestPlayback,

    /// <summary>Report device health and connection state. Venue devices only.</summary>
    ReportDeviceStatus
}

public enum PrincipalKind
{
    /// <summary>A signed-in member of the workspace that owns the session.</summary>
    WorkspaceUser,

    /// <summary>A paired venue device - Projector or Show Agent - on the band's own rig.</summary>
    VenueDevice,

    /// <summary>An anonymous phone that redeemed a session join code.</summary>
    AudienceParticipant
}

/// <summary>
/// The resolved identity behind a hub connection, with the capabilities it carries.
/// </summary>
public sealed record ConnectionPrincipal(
    PrincipalKind Kind,
    Role Role,
    string SessionCode,
    string? WorkspaceId,
    string Id,
    IReadOnlySet<Capability> Capabilities)
{
    public bool Can(Capability capability) => Capabilities.Contains(capability);

    /// <summary>A workspace member driving their own session.</summary>
    public static ConnectionPrincipal ForWorkspaceUser(string userId, string workspaceId, string sessionCode) =>
        new(PrincipalKind.WorkspaceUser, Role.Performer, sessionCode, workspaceId, userId,
            new HashSet<Capability>
            {
                Capability.Subscribe,
                Capability.IssueGameCommand,
                Capability.RequestPlayback
            });

    /// <summary>
    /// A paired device. It renders and plays what it is told and reports health; it never issues
    /// game commands, so a compromised device at a venue cannot drive somebody's show.
    /// </summary>
    public static ConnectionPrincipal ForVenueDevice(string deviceId, string workspaceId, string sessionCode, Role role) =>
        new(PrincipalKind.VenueDevice, role, sessionCode, workspaceId, deviceId,
            new HashSet<Capability>
            {
                Capability.Subscribe,
                Capability.ReportDeviceStatus
            });

    /// <summary>An audience phone: it may watch, and answer for itself. Nothing else.</summary>
    /// <remarks>
    /// Playback is deliberately not here. The Audience app's only play control was a developer
    /// panel that let any phone in the room start audio on the venue rig; the Performer drives
    /// playback, and it is now the only principal that can ask for it.
    /// </remarks>
    public static ConnectionPrincipal ForAudience(string participantId, string sessionCode) =>
        new(PrincipalKind.AudienceParticipant, Role.Audience, sessionCode, null, participantId,
            new HashSet<Capability>
            {
                Capability.Subscribe,
                Capability.SubmitAnswer
            });
}
