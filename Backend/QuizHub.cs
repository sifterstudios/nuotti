using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts;

namespace Backend;

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

    public Task SubmitAnswer(string session, int choiceIndex) =>
        Clients.Group(session).SendAsync("AnswerSubmitted", new AnswerSubmitted(Context.ConnectionId, choiceIndex));
}
