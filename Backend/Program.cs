using Backend;
using Microsoft.AspNetCore.SignalR;
using Nuotti.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// SignalR + JSON options
builder.Services
    .AddSignalR(o => { o.EnableDetailedErrors = true; })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = null);

// Dev CORS to allow Audience/Projector from other origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

var summaries = new[]
{
    "Freezing", "Bracing",
    "Chilly", "Cool",
    "Mild", "Warm",
    "Balmy", "Hot",
    "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

// SignalR hub and test endpoints (same shape as Nuotti.Backend)
app.MapHub<QuizHub>("/hub").RequireCors("AllowAll");
app.MapPost("/api/sessions/{name}", (string name) => Results.Ok(new SessionCreated(name, Guid.NewGuid().ToString()))).RequireCors("AllowAll");
app.MapPost("/api/pushQuestion/{session}", async (IHubContext<QuizHub> hub, string session, QuestionPushed q) =>
{
    await hub.Clients.Group(session).SendAsync("QuestionPushed", q);
    return Results.Accepted();
}).RequireCors("AllowAll");
app.MapPost("/api/play/{session}", async (IHubContext<QuizHub> hub, string session, PlayTrack cmd) =>
{
    await hub.Clients.Group(session).SendAsync("PlayTrack", cmd);
    return Results.Accepted();
}).RequireCors("AllowAll");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}