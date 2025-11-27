using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nuotti.Backend.Sessions;

namespace Nuotti.Backend.HealthChecks;

/// <summary>
/// Health check for SessionStore to ensure it's accessible and functional.
/// </summary>
internal class SessionStoreHealthCheck : IHealthCheck
{
    private readonly ISessionStore _sessionStore;

    public SessionStoreHealthCheck(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Touch minimal method to ensure store functions and does not throw
            _ = _sessionStore.GetCounts("__health__");
            return Task.FromResult(HealthCheckResult.Healthy("SessionStore is accessible"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("SessionStore is not accessible", ex));
        }
    }
}

