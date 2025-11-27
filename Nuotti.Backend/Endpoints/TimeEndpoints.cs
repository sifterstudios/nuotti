namespace Nuotti.Backend.Endpoints;

/// <summary>
/// Time-related endpoints for time drift detection.
/// </summary>
internal static class TimeEndpoints
{
    public static void MapTimeEndpoints(this WebApplication app)
    {
        // Get server time for time drift detection
        app.MapGet("/time", () =>
        {
            var serverTime = DateTimeOffset.UtcNow;
            return Results.Ok(new
            {
                serverTime = serverTime,
                serverTimeTicks = serverTime.Ticks,
                serverTimeIso8601 = serverTime.ToString("O")
            });
        }).RequireCors("NuottiCors");
    }
}

