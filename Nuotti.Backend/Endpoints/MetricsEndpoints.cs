using Nuotti.Backend.Metrics;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// Metrics endpoints exposing JSON metrics for observability.
/// </summary>
internal static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        app.MapGet("/metrics", (BackendMetrics metrics, ISessionStore sessionStore) =>
        {
            return Results.Json(
                metrics.Snapshot(sessionStore),
                new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
                },
                contentType: "application/json");
        })
        .RequireCors("NuottiCors");
    }
}

