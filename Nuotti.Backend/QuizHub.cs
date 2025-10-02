using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Event;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.Backend;

public class QuizHub : Hub
{
    // Single-parameter method used by projector and simple clients
    public async Task CreateOrJoin(string session)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, session);
    }

    // Named variant for clients that also provide an audience name (avoids overloading)
    public async Task CreateOrJoinWithName(string session, string audienceName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, session);
        await Clients.Group(session).SendAsync("JoinedAudience", new JoinedAudience(Context.ConnectionId, audienceName));
    }

    // TODO: Implement information so that we can properly populate AnswerSubmitted
    // public Task SubmitAnswer(string session, int choiceIndex) =>
    //     Clients.Group(session).SendAsync("AnswerSubmitted", new AnswerSubmitted(Context.ConnectionId, choiceIndex));

    // Audience can ask the projector to play a track. Projector will then call the REST API to actually play.
    public Task RequestPlay(string session, PlayTrack cmd) =>
        Clients.Group(session).SendAsync("RequestPlay", cmd);
}