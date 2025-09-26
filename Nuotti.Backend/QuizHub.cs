using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts;

namespace Nuotti.Backend;

public class QuizHub : Hub
{
    public Task CreateOrJoin(string session, string? audienceName = null)
    {
        _ = Groups.AddToGroupAsync(Context.ConnectionId, session);
        if (audienceName is not null)
            Clients.Group(session).SendAsync("JoinedAudience", new JoinedAudience(Context.ConnectionId, audienceName));
        return Task.CompletedTask;
    }

    public Task SubmitAnswer(string session, int choiceIndex) =>
        Clients.Group(session).SendAsync("AnswerSubmitted", new AnswerSubmitted(Context.ConnectionId, choiceIndex));
}