using System.Net.Http.Json;

namespace Nuotti.Backend.Workspaces;

/// <summary>
/// Why a magic link was issued. The two flows reach a recipient in very different states: a
/// sign-in link goes to someone who asked for it moments ago, an invitation may be the first
/// they have heard of Nuotti. Delivery cannot say the right thing without knowing which.
/// </summary>
public enum MagicLinkPurpose
{
    SignIn,
    Invitation
}

public interface IMagicLinkDelivery
{
    Task<bool> DeliverAsync(
        string email,
        IssuedMagicLink link,
        MagicLinkPurpose purpose,
        CancellationToken cancellationToken = default);
}

/// <summary>Delivers credentials to a configured internal email-service webhook.</summary>
public sealed class HttpMagicLinkDelivery(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<HttpMagicLinkDelivery> logger) : IMagicLinkDelivery
{
    public async Task<bool> DeliverAsync(
        string email,
        IssuedMagicLink link,
        MagicLinkPurpose purpose,
        CancellationToken cancellationToken = default)
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
                .PostAsJsonAsync(
                    uri,
                    new { email, link.Token, link.ExpiresAt, Purpose = purpose.ToString() },
                    cancellationToken);
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
