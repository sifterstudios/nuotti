using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace Nuotti.Backend.Workspaces;

/// <summary>
/// Delivers magic links as email through Mailgun's HTTP sending API.
/// </summary>
/// <remarks>
/// Mailgun's messages endpoint takes HTTP Basic auth (user "api") and form-encoded fields, so it
/// cannot be reached by pointing <see cref="HttpMagicLinkDelivery"/> at it - that one posts
/// unauthenticated JSON to an internal webhook. The two are alternatives, selected by config.
/// </remarks>
public sealed class MailgunMagicLinkDelivery(
    IHttpClientFactory clients,
    IOptions<MailgunOptions> options,
    ILogger<MailgunMagicLinkDelivery> logger) : IMagicLinkDelivery
{
    public const string HttpClientName = "mailgun";

    public async Task<bool> DeliverAsync(
        string email,
        IssuedMagicLink link,
        MagicLinkPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogError("Mailgun delivery is not configured");
            return false;
        }

        var url = BuildSignInUrl(settings.SignInUrl!, link.Token);
        var (subject, text, html) = Compose(settings.ProductName, purpose, url, link.ExpiresAt);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.BaseUrl!.TrimEnd('/')}/v3/{settings.Domain}/messages")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["from"] = settings.From!,
                ["to"] = email,
                ["subject"] = subject,
                ["text"] = text,
                ["html"] = html
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{settings.ApiKey}")));

        try
        {
            using var response = await clients.CreateClient(HttpClientName)
                .SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;

            // The body carries Mailgun's reason (unverified domain, wrong region, over quota).
            // Without it every failure looks identical in the log.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Mailgun delivery failed with status {StatusCode}: {Body}",
                response.StatusCode,
                Truncate(body, 500));
            return false;
        }
        catch (System.Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Mailgun delivery failed");
            return false;
        }
    }

    /// <summary>Appends the token to the configured landing URL, escaped for a query string.</summary>
    public static string BuildSignInUrl(string signInUrl, string token)
    {
        var builder = new UriBuilder(signInUrl);
        var query = HttpUtility.ParseQueryString(builder.Query);
        query["token"] = token;
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }

    /// <summary>Builds the subject and bodies for a purpose. Pure, so the copy can be tested.</summary>
    public static (string Subject, string Text, string Html) Compose(
        string product, MagicLinkPurpose purpose, string url, DateTimeOffset expiresAt)
    {
        var minutes = Math.Max(1, (int)Math.Round((expiresAt - DateTimeOffset.UtcNow).TotalMinutes));
        var (subject, lead) = purpose switch
        {
            MagicLinkPurpose.Invitation => (
                $"You have been invited to {product}",
                $"Someone invited you to their workspace on {product}, a live music-guessing show platform. Open the link below to accept and set up your account."),
            _ => (
                $"Your {product} sign-in link",
                $"Use the link below to sign in to {product}.")
        };

        var text =
            $"{lead}\n\n{url}\n\nThis link expires in about {minutes} minutes and can only be used once.\n" +
            "If you did not request it, you can ignore this email.";

        var html =
            $"""
             <p>{HttpUtility.HtmlEncode(lead)}</p>
             <p><a href="{HttpUtility.HtmlAttributeEncode(url)}">{HttpUtility.HtmlEncode(subject)}</a></p>
             <p>This link expires in about {minutes} minutes and can only be used once.<br>
             If you did not request it, you can ignore this email.</p>
             """;

        return (subject, text, html);
    }

    static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";
}
