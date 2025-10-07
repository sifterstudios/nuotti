using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using System.Text.Json;
namespace Nuotti.Backend.Exception;

/// <summary>
/// Helpers to emit NuottiProblem results from Minimal APIs.
/// </summary>
public static class ProblemResults
{
    public static IResult BadRequest(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => Json(new NuottiProblem(title, 400, detail, reason, field, correlationId), 400);

    public static IResult Conflict(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => Json(new NuottiProblem(title, 409, detail, reason, field, correlationId), 409);

    public static IResult UnprocessableEntity(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => Json(new NuottiProblem(title, 422, detail, reason, field, correlationId), 422);

    public static IResult Forbidden(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => Json(new NuottiProblem(title, 403, detail, reason, field, correlationId), 403);
    
    static IResult Json(object value, int statusCode)
        => Results.Json(value, ContractsJson.RestOptions, statusCode: statusCode);
    
    public static IResult WrongRoleTriedExecutingResult(Role role)
    {
        return ProblemResults.Forbidden(
            title: "Unauthorized Role",
            detail: $"Only {role.ToString()} may execute this command.",
            reason: ReasonCode.UnauthorizedRole,
            field: "issuedByRole");
    }
}
/// <summary>
/// Minimal API middleware that maps common exceptions to NuottiProblem payloads.
/// </summary>
public sealed class ProblemHandlingMiddleware(RequestDelegate next)
{
    const string CorrelationHeader = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (System.Exception ex)
        {
            // Resolve correlation id from header if present or generate a new one
            Guid? correlationId = null;
            if (context.Request.Headers.TryGetValue(CorrelationHeader, out var values))
            {
                if (Guid.TryParse(values.ToString(), out var parsed))
                    correlationId = parsed;
            }

            var (status, title, detail, reason, field) = MapException(ex);
            var problem = new NuottiProblem(title, status, detail, reason, field, correlationId);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, ContractsJson.RestOptions);
        }
    }

    static (int status, string title, string detail, ReasonCode reason, string? field) MapException(System.Exception ex)
    {
        return ex switch
        {
            ArgumentException aex => (400, "Invalid argument", aex.Message, ReasonCode.InvalidStateTransition, aex.ParamName),
            UnauthorizedAccessException uex => (403, "Unauthorized role", uex.Message, ReasonCode.UnauthorizedRole, null),
            InvalidOperationException iex => (409, "Conflict", iex.Message, ReasonCode.DuplicateCommand, null),
            _ => (500, "Unexpected error", "An unexpected error occurred.", ReasonCode.None, null)
        };
    }

}