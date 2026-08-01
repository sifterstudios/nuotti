using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Commands;
using Nuotti.Backend.Participants;
using Nuotti.Backend.RateLimiting;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Backend;

public class QuizHub(
    ILogger<QuizHub> logger,
    ILogStreamer log,
    ISessionStore sessions,
    ISessionCommandProcessor processor,
    IParticipantIdentityStore participants,
    ISessionWorkspaceBinder? workspaces = null) : Hub
{
    const string SessionKey = "session";
    const string RoleKey = "role";
    const string ParticipantKey = "participantId";

    // Engine reports status changes via hub; broadcast to entire session
    public Task EngineStatusChanged(string session, EngineStatusChanged evt)
        => Clients.Group(session).SendAsync("EngineStatusChanged", evt);

    // Performer can ping engine; relay to engine group
    public Task Ping(string session, long clientTicks)
        => Clients.Group($"{session}:engine").SendAsync("Ping", clientTicks);

    // Engine echoes back; relay to performer group
    public Task Echo(string session, long clientTicks, long engineTicks)
        => Clients.Group($"{session}:performer").SendAsync("Echo", clientTicks, engineTicks);

    public async Task Join(string session, string role, string? name, string? deviceSecret)
    {
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
        await Groups.AddToGroupAsync(Context.ConnectionId, session);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{session}:{normalizedRole.ToLowerInvariant()}");
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
            await Clients.Group(session).SendAsync("JoinedAudience",
                new JoinedAudience(participantId ?? Context.ConnectionId, displayName));
        }
    }

    public Task CreateOrJoinWithName(string session, string audienceName, string deviceSecret)
        => Join(session, role: "audience", name: audienceName, deviceSecret: deviceSecret);

    public async override Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: "Connected",
            ConnectionId: Context.ConnectionId
        ));
    }

    public async override Task OnDisconnectedAsync(System.Exception? exception)
    {
        var session = Context.Items.TryGetValue(SessionKey, out var sessionObject) ? sessionObject as string : null;
        var role = Context.Items.TryGetValue(RoleKey, out var roleObject) ? roleObject as string : null;

        if (!string.IsNullOrWhiteSpace(session))
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, session);
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

    public async Task RequestPlay(string session, PlayTrack cmd)
    {
        var role = Context.Items.TryGetValue(RoleKey, out var roleObj) ? roleObj as string : null;
        if (!string.Equals(role, "audience", StringComparison.OrdinalIgnoreCase))
        {
            await SendProblemAsync(NuottiProblem.UnprocessableEntity(
                title: "Unauthorized role",
                detail: "Only audience members can request playback.",
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

        await Clients.Group(session).SendAsync("RequestPlay", cmd);
    }

    // Audience submits an answer. Pass Guid.Empty to mint a fresh CommandId; otherwise retry with the same id.
    public async Task SubmitAnswer(string session, int choiceIndex, Guid commandId)
    {
        var role = Context.Items.TryGetValue(RoleKey, out var roleObj) ? roleObj as string : null;
        if (!string.Equals(role, "audience", StringComparison.OrdinalIgnoreCase))
        {
            await SendProblemAsync(NuottiProblem.UnprocessableEntity(
                title: "Business rule violated",
                detail: "Performer cannot submit an answer.",
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
