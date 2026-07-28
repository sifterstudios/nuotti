using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nuotti.Contracts.V1;
using System.Text.Json;

namespace Nuotti.Backend.Endpoints;

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Standardized health check endpoints using ASP.NET Core health checks infrastructure
        // /health/live - liveness probe (app is running)
        // /health/ready - readiness probe (app is ready to accept traffic)
        
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteProbeResult("live"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        })
        .RequireCors("NuottiCors");

        // Readiness: verify required dependencies (SignalR, SessionStore)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = WriteProbeResult("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        })
        .RequireCors("NuottiCors");
    }

    /// <summary>
    /// Writes which probe answered alongside its status. The default writer emits only the bare
    /// status word, so "/health/live" and "/health/ready" were indistinguishable in a response.
    /// </summary>
    static Func<HttpContext, HealthReport, Task> WriteProbeResult(string probe)
        => async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var payload = new
            {
                probe,
                status = report.Status.ToString(),
                totalDurationMs = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description
                })
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, ContractsJson.RestOptions));
        };
}