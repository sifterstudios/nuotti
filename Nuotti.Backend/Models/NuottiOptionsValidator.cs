using Microsoft.Extensions.Options;

namespace Nuotti.Backend.Models;

/// <summary>
/// Validates NuottiOptions configuration.
/// </summary>
public class NuottiOptionsValidator : IValidateOptions<NuottiOptions>
{
    public ValidateOptionsResult Validate(string? name, NuottiOptions options)
    {
        var errors = new List<string>();

        // Validate timeouts are positive
        if (options.SessionIdleTimeoutSeconds <= 0)
        {
            errors.Add($"SessionIdleTimeoutSeconds must be positive (current: {options.SessionIdleTimeoutSeconds}). " +
                      "Hint: Set NUOTTI_SESSIONIDLETIMEOUTSECONDS environment variable or add to appsettings.json");
        }

        if (options.SessionEvictionIntervalSeconds <= 0)
        {
            errors.Add($"SessionEvictionIntervalSeconds must be positive (current: {options.SessionEvictionIntervalSeconds}). " +
                      "Hint: Set NUOTTI_SESSIONEVICTIONINTERVALSECONDS environment variable or add to appsettings.json");
        }

        // Validate idempotency settings
        if (options.IdempotencyTtlSeconds <= 0)
        {
            errors.Add($"IdempotencyTtlSeconds must be positive (current: {options.IdempotencyTtlSeconds}). " +
                      "Hint: Set NUOTTI_IDEMPOTENCYTTLSECONDS environment variable or add to appsettings.json");
        }

        if (options.IdempotencyMaxPerSession <= 0)
        {
            errors.Add($"IdempotencyMaxPerSession must be positive (current: {options.IdempotencyMaxPerSession}). " +
                      "Hint: Set NUOTTI_IDEMPOTENCYMAXPERSESSION environment variable or add to appsettings.json");
        }

        // Validate alerting settings
        if (options.MissingRoleAlertThresholdSeconds <= 0)
        {
            errors.Add($"MissingRoleAlertThresholdSeconds must be positive (current: {options.MissingRoleAlertThresholdSeconds}). " +
                      "Hint: Set NUOTTI_MISSINGROLEALERTTHRESHOLDSECONDS environment variable or add to appsettings.json");
        }

        // Validate CORS origins in production
        if (string.IsNullOrWhiteSpace(options.AllowedOrigins) && name == Options.DefaultName)
        {
            // This is just a warning, not an error, since it only matters in non-dev environments
            // We'll log it but not fail validation
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}

