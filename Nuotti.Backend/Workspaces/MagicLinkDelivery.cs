using System.Net.Http.Json;

namespace Nuotti.Backend.Workspaces;

public interface IMagicLinkDelivery
{
    Task<bool> DeliverAsync(string email, IssuedMagicLink link, CancellationToken cancellationToken = default);
}

/// <summary>Delivers credentials to a configured internal email-service webhook.</summary>
public sealed class HttpMagicLinkDelivery(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<HttpMagicLinkDelivery> logger) : IMagicLinkDelivery
{
    public async Task<bool> DeliverAsync(string email, IssuedMagicLink link, CancellationToken cancellationToken = default)
    {
        var destination = configuration["Nuotti:MagicLinkDeliveryUrl"];
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri))
        {
            logger.LogError("Magic-link delivery is not configured");
            return false;
        }

        try
        {
            using var response = await clients.CreateClient(nameof(HttpMagicLinkDelivery))
                .PostAsJsonAsync(uri, new { email, link.Token, link.ExpiresAt }, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            logger.LogError("Magic-link delivery failed with status {StatusCode}", response.StatusCode);
            return false;
        }
        catch (System.Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Magic-link delivery failed");
            return false;
        }
    }
}
