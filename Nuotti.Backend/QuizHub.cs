using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
using Nuotti.Contracts.V1.Event;
namespace Nuotti.Backend;

public class QuizHub(ILogger<QuizHub> logger, ILogStreamer log, Nuotti.Backend.Sessions.ISessionStore sessions) : Hub
{
    const string SessionKey = "session";
    const string RoleKey = "role";

    public async Task Join(string session, string role, string? name = null)
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
        Context.Items[SessionKey] = session;
        Context.Items[RoleKey] = role;

        await Groups.AddToGroupAsync(Context.ConnectionId, session);
        // Track connection by role in session store
        sessions.Touch(session, role, Context.ConnectionId, name);

        logger.LogInformation("Join: conn={ConnectionId} session={Session} role={Role} name={Name}", Context.ConnectionId, session, role, name);
        await log.BroadcastAsync(new LogEvent(
            Timestamp: DateTimeOffset.UtcNow,
            Level: "Info",
            Source: nameof(QuizHub),
            Message: $"Join: name={name}",
            ConnectionId: Context.ConnectionId,
            Session: session,
            Role: role
        ));

        if (!string.IsNullOrWhiteSpace(name))
        {
            await Clients.Group(session).SendAsync("JoinedAudience", new JoinedAudience(Context.ConnectionId, name!));
        }
    }

    public Task CreateOrJoinWithName(string session, string audienceName) => Join(session, role: "audience", name: audienceName);

    public override async Task OnConnectedAsync()
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

    public async override Task OnDisconnectedAsync(Exception? exception)
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
        // Remove from session store
        sessions.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Emit a NuottiProblem to the calling client
    Task SendProblemAsync(NuottiProblem problem)
        => Clients.Caller.SendAsync("Problem", problem);

    // Audience can ask the projector to play a track. Projector will then call the REST API to actually play.
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
}