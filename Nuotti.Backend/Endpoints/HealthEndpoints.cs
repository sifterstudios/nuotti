using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.Endpoints;

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new
            {
                status = "live"
            }))
            .RequireCors("AllowAll");

        // Readiness: verify required dependencies are accessible
        app.MapGet("/health/ready", (IHubContext<QuizHub> hub, ISessionStore store) =>
            {
                try
                {
                    // Touch minimal method to ensure store functions and does not throw
                    _ = store.GetCounts("__health__");

                    return Results.Ok(new
                    {
                        status = "ready"
                    });
                }
                catch
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireCors("AllowAll");
    }
}