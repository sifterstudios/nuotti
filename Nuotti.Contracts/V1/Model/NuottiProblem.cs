using System.Text.Json.Serialization;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// A minimal, stable ProblemDetails-like shape shared across services.
/// Field names are stable and serialized with camelCase under ContractsJson.DefaultOptions.
/// </summary>
public sealed record NuottiProblem(
    string Title,
    int Status,
    string Detail,
    ReasonCode Reason,
    string? Field = null,
    Guid? CorrelationId = null)
{
    /// <summary>
    /// Factory for HTTP 400 Bad Request problems.
    /// </summary>
    public static NuottiProblem BadRequest(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => new(title, 400, detail, reason, field, correlationId);

    /// <summary>
    /// Factory for HTTP 409 Conflict problems.
    /// </summary>
    public static NuottiProblem Conflict(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => new(title, 409, detail, reason, field, correlationId);

    /// <summary>
    /// Factory for HTTP 422 Unprocessable Entity problems.
    /// </summary>
    public static NuottiProblem UnprocessableEntity(string title, string detail, ReasonCode reason = ReasonCode.None, string? field = null, Guid? correlationId = null)
        => new(title, 422, detail, reason, field, correlationId);
}
