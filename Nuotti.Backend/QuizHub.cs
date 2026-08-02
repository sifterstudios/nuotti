using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Participants;
using Nuotti.Backend.RateLimiting;
using Nuotti.Backend.Realtime;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Backend;

/// <summary>
/// The one realtime surface, mapped at /hub in every environment.
/// </summary>
/// <remarks>
/// What a connection may do is decided by the credential it presents, not by the role it claims:
/// <see cref="OnConnectedAsync"/> resolves a <see cref="ConnectionPrincipal"/> and every mutating
/// method checks a <see cref="Capability"/>. Before that, Join took a role as a string and believed
/// it, which is why this hub could only ever be mapped in Development and why the deployed build
/// had no working realtime path at all.
/// </remarks>
public class QuizHub(
    ILogger<QuizHub> logger,
    ILogStreamer log,
    ISessionStore sessions,
    ISessionCommandProcessor processor,
    IParticipantIdentityStore participants,
    ISessionWorkspaceBinder? workspaces = null,
    IConnectionPrincipalResolver? principals = null,
    IOptions<RealtimeOptions>? realtimeOptions = null) : Hub
{
    const string SessionKey = "session";
    const string RoleKey = "role";
    const string ParticipantKey = "participantId";
    const string PrincipalKey = "principal";


    ConnectionPrincipal? Principal()
        => Context.Items.TryGetValue(PrincipalKey, out var value) ? value as ConnectionPrincipal : null;

    /// <summary>
    /// The session this call acts on. A credentialled connection acts on its own session and
    /// nothing else; the argument is only honoured for connections admitted without a credential.
    /// </summary>
    /// <remarks>
    /// These three relays took a session name and forwarded to it, so any connected client could
    /// push engine status into, or ping, a session it had nothing to do with.
    /// </remarks>
    string ScopeToPrincipal(string session) => Principal()?.SessionCode ?? session;

    // Engine reports status changes via hub; broadcast to entire session.
    public async Task EngineStatusChanged(string session, EngineStatusChanged evt)
    {
        if (!Allows(Capability.ReportDeviceStatus, legacyRole: "engine"))
        {
            await SendProblemAsync(NuottiProblem.UnprocessableEntity(
                title: "Unauthorized role",
                detail: "Only a venue device reports engine status.",
                reason: ReasonCode.UnauthorizedRole,
                field: "role"));
            return;
        }
        await Clients.Group(RealtimeGroups.Session(ScopeToPrincipal(session))).SendAsync("EngineStatusChanged", evt);
    }

    // Performer can ping engine; relay to engine group
    public Task Ping(string session, long clientTicks)
        => Clients.Group(RealtimeGroups.SessionRole(ScopeToPrincipal(session), "engine")).SendAsync("Ping", clientTicks);

    // Engine echoes back; relay to performer group
    public Task Echo(string session, long clientTicks, long engineTicks)
        => Clients.Group(RealtimeGroups.SessionRole(ScopeToPrincipal(session), "performer"))
            .SendAsync("Echo", clientTicks, engineTicks);

    public async Task Join(string session, string role, string? name, string? deviceSecret)
    {
        var principal = Principal();

        // A credentialled connection already knows which session and role it is. The arguments
        // survive only because four shipped clients still send them, and they are no longer
        // trusted for anything: a phone cannot declare itself the Performer of somebody's show.
        if (principal is not null)
        {
            session = principal.SessionCode;
            role = principal.Role.ToString();
        }

        if (string.IsNullOrWhiteSpace(session))
        {
            await SendProblemAsync(NuottiProblem.BadRequest(
                title: "Invalid session",
                detail: "Session code must be provided.",
                reason: ReasonCode.InvalidStateTransition,
                field: "session"));
            return;
        }
        if (string.IsNullOrWhiteSpace(role))
        {
            await SendProblemAsync(NuottiProblem.BadRequest(
                title: "Invalid role",
                detail: "Role must be provided.",
                reason: ReasonCode.UnauthorizedRole,
                field: "role"));
            return;
        }

        // Track session and role on the connection
        var normalizedRole = role.Trim();
        Context.Items[SessionKey] = session;
        Context.Items[RoleKey] = normalizedRole;

        string? participantId = null;
        string? displayName = name;
        if (string.Equals(normalizedRole, "audience", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(deviceSecret))
            {
                await SendProblemAsync(NuottiProblem.BadRequest(
                    title: "Device identity required",
                    detail: "Audience joins require a device-bound secret for Session-scoped reconnect.",
                    reason: ReasonCode.InvalidStateTransition,
                    field: "deviceSecret"));
                return;
            }

            try
            {
                var participant = participants.JoinOrRestore(session, deviceSecret, name);
                participantId = participant.ParticipantId;
                displayName = participant.DisplayName;
                Context.Items[ParticipantKey] = participantId;
            }
            catch (ArgumentException ex)
            {
                await SendProblemAsync(NuottiProblem.BadRequest(
                    title: "Invalid display name",
                    detail: ex.Message,
                    reason: ReasonCode.InvalidStateTransition,
                    field: "name"));
                return;
            }
        }

        // Join session-wide group and session+role group
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Session(session));
        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.SessionRole(session, normalizedRole));
        // Track connection by role in the session store
        sessions.Touch(session, normalizedRole, Context.ConnectionId, displayName);

        // Send server time to client for time drift detection
        var serverTime = DateTimeOffset.UtcNow;
        await Clients.Caller.SendAsync("ServerTime", serverTime.Ticks, serverTime.ToString("O"));

        if (participantId is not null)
        {
            await Clients.Caller.SendAsync("ParticipantRestored", new
            {
                ParticipantId = participantId,
                SessionCode = session,
                DisplayName = displayName
            });
        }

        logger.LogInformation("Join: conn={ConnectionId} session={Session} role={Role} name={Name} participant={ParticipantId}",
            Context.ConnectionId, session, role, displayName, participantId);
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: $"Join: name={displayName}",
            ConnectionId: Context.ConnectionId,
            Session: session,
            Role: role
        ));

        if (!string.IsNullOrWhiteSpace(displayName) && string.Equals(normalizedRole, "audience", StringComparison.OrdinalIgnoreCase))
        {
            await Clients.Group(RealtimeGroups.Session(session)).SendAsync("JoinedAudience",
                new JoinedAudience(participantId ?? Context.ConnectionId, displayName));
        }
    }

    public Task CreateOrJoinWithName(string session, string audienceName, string deviceSecret)
        => Join(session, role: "audience", name: audienceName, deviceSecret: deviceSecret);

    public async override Task OnConnectedAsync()
    {
        // Resolution happens before anything else, because an unrecognised connection must not
        // reach a hub method at all. Throwing rather than aborting is what makes the refusal
        // visible: an abort races the handshake, so the client can see StartAsync succeed and then
        // silently retry forever against a hub it will never be allowed into.
        if (principals is not null && !await TryAdmitAsync())
            throw new HubException("This connection presented no credential this session recognises.");

        await base.OnConnectedAsync();
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: "Connected",
            ConnectionId: Context.ConnectionId
        ));
    }

    /// <summary>
    /// Resolves the connection's credential. Returns false when the connection may not proceed.
    /// </summary>
    async Task<bool> TryAdmitAsync()
    {
        var http = Context.GetHttpContext();
        var query = http?.Request.Query;
        var token = query?["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authorization = http?.Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            token = authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
                ? authorization[prefix.Length..].Trim()
                : null;
        }

        // "session" is what the shipped clients already send; "sessionCode" is what the Workspace
        // surface uses. Accepting both keeps one hub compatible with both vocabularies.
        var sessionCode = query?["sessionCode"].ToString();
        if (string.IsNullOrWhiteSpace(sessionCode)) sessionCode = query?["session"].ToString();

        var principal = await principals!.ResolveAsync(new RealtimeConnectionRequest(
            token, sessionCode, query?["workspaceId"].ToString(), query?["deviceRole"].ToString()),
            Context.ConnectionAborted);

        if (principal is null)
        {
            if (realtimeOptions?.Value.AllowUnauthenticatedConnections == true) return true;
            logger.LogWarning("Rejected realtime connection with no usable credential. conn={ConnectionId}",
                Context.ConnectionId);
            return false;
        }

        Context.Items[PrincipalKey] = principal;
        Context.Items[SessionKey] = principal.SessionCode;
        Context.Items[RoleKey] = principal.Role.ToString();
        if (principal.Kind == PrincipalKind.AudienceParticipant) Context.Items[ParticipantKey] = principal.Id;

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Session(principal.SessionCode));
        await Groups.AddToGroupAsync(Context.ConnectionId,
            RealtimeGroups.SessionRole(principal.SessionCode, principal.Role.ToString()));

        // Workspace-scoped publications fan out to their own group, so a member watching from the
        // Performer app receives the same stream the venue rig does.
        if (!string.IsNullOrWhiteSpace(principal.WorkspaceId))
            await Groups.AddToGroupAsync(Context.ConnectionId,
                RealtimeGroups.Workspace(principal.WorkspaceId, principal.SessionCode));

        sessions.Touch(principal.SessionCode, principal.Role.ToString(), Context.ConnectionId, null);
        return true;
    }

    public async override Task OnDisconnectedAsync(System.Exception? exception)
    {
        var session = Context.Items.TryGetValue(SessionKey, out var sessionObject) ? sessionObject as string : null;
        var role = Context.Items.TryGetValue(RoleKey, out var roleObject) ? roleObject as string : null;

        if (!string.IsNullOrWhiteSpace(session))
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Session(session));
            }
            catch
            {
                // ignore errors during cleanup
            }
        }

        logger.LogInformation("Disconnected: conn={ConnectionId} session={Session} role={Role}", Context.ConnectionId, session, role);
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: "Disconnected",
            ConnectionId: Context.ConnectionId,
            Session: session,
            Role: role
        ));
        sessions.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    Task SendProblemAsync(NuottiProblem problem)
        => Clients.Caller.SendAsync("Problem", problem);

    /// <summary>
    /// Whether this connection carries a capability. Connections admitted without a credential
    /// fall back to the role string they claimed, which is exactly as weak as it sounds and is why
    /// <see cref="RealtimeOptions.AllowUnauthenticatedConnections"/> is off outside the local loop.
    /// </summary>
    bool Allows(Capability capability, string legacyRole)
    {
        if (Principal() is { } principal) return principal.Can(capability);
        var role = Context.Items.TryGetValue(RoleKey, out var roleObject) ? roleObject as string : null;
        return string.Equals(role, legacyRole, StringComparison.OrdinalIgnoreCase);
    }

    public async Task RequestPlay(string session, PlayTrack cmd)
    {
        if (Principal() is { } admitted) session = admitted.SessionCode;

        if (!Allows(Capability.RequestPlayback, legacyRole: "performer"))
        {
            await SendProblemAsync(NuottiProblem.UnprocessableEntity(
                title: "Unauthorized role",
                detail: "This connection may not request playback.",
                reason: ReasonCode.UnauthorizedRole,
                field: "role"));
            return;
        }

        if (!ConnectionRateLimiter.TryAllow(Context.ConnectionId, "PlayStop", TimeSpan.FromSeconds(2)))
        {
            await SendProblemAsync(new NuottiProblem(
                Title: "Too Many Requests",
                Status: 429,
                Detail: "You are sending play/stop actions too quickly. Please wait a moment and try again.",
                Reason: ReasonCode.None,
                Field: null,
                CorrelationId: null));
            return;
        }

        var role = Context.Items.TryGetValue(RoleKey, out var roleObj) ? roleObj as string : null;
        logger.LogInformation("RequestPlay: conn={ConnectionId} session={Session} role={Role} url={Url}", Context.ConnectionId, session, role, cmd.FileUrl);
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: $"RequestPlay: url={cmd.FileUrl}",
            ConnectionId: Context.ConnectionId,
            Session: session,
            Role: role
        ));

        await Clients.Group(RealtimeGroups.Session(session)).SendAsync("RequestPlay", cmd);
    }

    // Audience submits an answer. Pass Guid.Empty to mint a fresh CommandId; otherwise retry with the same id.
    public async Task SubmitAnswer(string session, int choiceIndex, Guid commandId)
    {
        if (Principal() is { } admitted) session = admitted.SessionCode;

        if (!Allows(Capability.SubmitAnswer, legacyRole: "audience"))
        {
            await SendProblemAsync(NuottiProblem.UnprocessableEntity(
                title: "Business rule violated",
                detail: "Only an audience member can submit an answer.",
                reason: ReasonCode.InvalidStateTransition,
                field: "role"));
            return;
        }

        if (!ConnectionRateLimiter.TryAllow(Context.ConnectionId, "SubmitAnswer", TimeSpan.FromMilliseconds(500)))
        {
            await SendProblemAsync(new NuottiProblem(
                Title: "Too Many Requests",
                Status: 429,
                Detail: "You are submitting answers too quickly. Please wait and try again.",
                Reason: ReasonCode.None,
                Field: null,
                CorrelationId: null));
            return;
        }

        var audienceId = Context.Items.TryGetValue(ParticipantKey, out var partObj) && partObj is string partId
            ? partId
            : Context.ConnectionId;
        var resolvedCommandId = commandId == Guid.Empty ? Guid.NewGuid() : commandId;
        var cmd = new SubmitAnswer(SongId: null, ChoiceIndex: choiceIndex)
        {
            SessionCode = session,
            IssuedByRole = Role.Audience,
            IssuedById = audienceId,
            CommandId = resolvedCommandId
        };

        logger.LogInformation("SubmitAnswer: conn={ConnectionId} session={Session} choiceIndex={ChoiceIndex} CommandId={CommandId} audience={AudienceId}",
            Context.ConnectionId, session, choiceIndex, cmd.CommandId, audienceId);

        var workspaceId = workspaces?.Resolve(session) ?? "legacy";
        var result = await processor.ApplyAsync(session, Actor.Verified(Role.Audience, audienceId), cmd,
            workspaceId: workspaceId);
        if (result.Problem is not null)
        {
            await SendProblemAsync(result.Problem);
        }
        else
        {
            await Clients.Caller.SendAsync("AnswerAccepted", new
            {
                CommandId = cmd.CommandId,
                ChoiceIndex = choiceIndex,
                Outcome = result.Outcome.ToString()
            });
        }
    }
}
