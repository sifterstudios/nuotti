using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.RateLimiting;
using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Eventing;
using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Backend;

public class QuizHub(ILogger<QuizHub> logger, ILogStreamer log, ISessionStore sessions, IEventBus bus) : Hub
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
        var normalizedRole = role.Trim();
        Context.Items[SessionKey] = session;
        Context.Items[RoleKey] = normalizedRole;

        // Join session-wide group and session+role group
        await Groups.AddToGroupAsync(Context.ConnectionId, session);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{session}:{normalizedRole.ToLowerInvariant()}");
        // Track connection by role in the session store
        sessions.Touch(session, normalizedRole, Context.ConnectionId, name);

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
            await Clients.Group(session).SendAsync("JoinedAudience", new JoinedAudience(Context.ConnectionId, name));
        }
    }

    public Task CreateOrJoinWithName(string session, string audienceName) => Join(session, role: "audience", name: audienceName);

    // // Compatibility shim for older clients (e.g., AudioEngine) that invoke 'CreateOrJoin'
    // public Task CreateOrJoin(string session) => Join(session, role: "engine");
    //
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
        // Remove from the session store
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

        // Rate limit play/stop actions to 1 per 2 seconds per connection
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

    // Audience submits answer choice
    public async Task SubmitAnswer(string session, int choiceIndex)
    {
        // Only audience members may submit answers
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

        // Debounce SubmitAnswer per-connection with 500ms window
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

        var audienceId = Context.ConnectionId;
        var evt = new AnswerSubmitted(audienceId, choiceIndex)
        {
            AudienceId = audienceId,
            ChoiceIndex = choiceIndex,
            CorrelationId = Guid.NewGuid(),
            CausedByCommandId = Guid.NewGuid(),
            SessionCode = session
        };
        await bus.PublishAsync(evt);
    }
}