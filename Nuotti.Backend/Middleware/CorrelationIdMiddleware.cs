using Serilog.Context;
namespace Nuotti.Backend.Middleware;

/// <summary>
/// Middleware that extracts or generates correlation ID from X-Correlation-Id header
/// and stores it in HttpContext.Items and Serilog LogContext for use throughout the request pipeline.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    const string CorrelationHeader = "X-Correlation-Id";
    const string CorrelationIdItemKey = "CorrelationId";

    public async Task Invoke(HttpContext context)
    {
        // Extract correlation ID from header or generate a new one
        Guid correlationId;
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var values))
        {
            var headerValue = values.ToString();
            if (Guid.TryParse(headerValue, out var parsed))
            {
                correlationId = parsed;
            }
            else
            {
                // Invalid format, generate new one
                correlationId = Guid.NewGuid();
            }
        }
        else
        {
            // No header, generate new one
            correlationId = Guid.NewGuid();
        }

        // Store in HttpContext.Items for use throughout the request
        context.Items[CorrelationIdItemKey] = correlationId;

        // Also add to response headers so clients can see it
        context.Response.Headers.Append(CorrelationHeader, correlationId.ToString());

        // Push correlation ID into Serilog LogContext so all logs in this request include it
        using (LogContext.PushProperty("correlationId", correlationId.ToString()))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Gets the correlation ID from HttpContext.Items if present.
    /// </summary>
    public static Guid? GetCorrelationId(HttpContext? context)
    {
        if (context?.Items.TryGetValue(CorrelationIdItemKey, out var value) == true && value is Guid guid)
        {
            return guid;
        }
        return null;
    }
}

