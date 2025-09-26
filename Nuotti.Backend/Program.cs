using Nuotti.Backend;
using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

var sessions = new Dictionary<string, HashSet<string>>(); // sessionCode -> connIds

app.MapHub<QuizHub>("/hub");
app.MapPost("/api/sessions/{name}", (string name) => Results.Ok(new SessionCreated(name, Guid.NewGuid().ToString())));
app.MapPost("/api/pushQuestion/{session}", async (IHubContext<QuizHub> hub, string session, QuestionPushed q) =>
{
    await hub.Clients.Group(session).SendAsync("QuestionPushed", q);
    return Results.Accepted();
});
app.MapPost("/api/play/{session}", async (IHubContext<QuizHub> hub, string session, PlayTrack cmd) =>
{
    await hub.Clients.Group(session).SendAsync("PlayTrack", cmd);
    return Results.Accepted();
});

app.Run();