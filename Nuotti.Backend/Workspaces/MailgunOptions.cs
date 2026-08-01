using Microsoft.Extensions.Options;

namespace Nuotti.Backend.Workspaces;

/// <summary>
/// Mailgun sending configuration, bound from the "Nuotti:Mailgun" section.
/// Environment variables use the double-underscore form, e.g. Nuotti__Mailgun__ApiKey.
/// </summary>
public sealed class MailgunOptions
{
    public const string SectionName = "Nuotti:Mailgun";

    /// <summary>Private API key. Mailgun authenticates it as HTTP Basic user "api".</summary>
    public string? ApiKey { get; init; }

    /// <summary>The verified sending domain, e.g. "mg.nuotti.app".</summary>
    public string? Domain { get; init; }

    /// <summary>RFC 5322 From header, e.g. "Nuotti &lt;no-reply@nuotti.app&gt;".</summary>
    public string? From { get; init; }

    /// <summary>
    /// Regional API host. Mailgun serves EU-resident accounts from a separate hostname, and a
    /// valid key presented to the wrong region fails authentication rather than saying so.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.eu.mailgun.net";

    /// <summary>
    /// Where the recipient lands, e.g. "https://performer.nuotti.app/signin". The token is
    /// appended as a query parameter. The Backend issues a bare token and never a URL, so this
    /// is the only place that knows what a magic link actually points at.
    /// </summary>
    public string? SignInUrl { get; init; }

    /// <summary>Product name used in subject lines and body copy.</summary>
    public string ProductName { get; init; } = "Nuotti";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Domain)
        && !string.IsNullOrWhiteSpace(From)
        && !string.IsNullOrWhiteSpace(SignInUrl);
}

public sealed class MailgunOptionsValidator : IValidateOptions<MailgunOptions>
{
    public ValidateOptionsResult Validate(string? name, MailgunOptions options)
    {
        // An unset section is valid: it simply means Mailgun delivery is not in use. A partially
        // set one is not - it would fail at the moment someone tries to sign in, which is the
        // worst possible time to discover a typo.
        if (!options.HasAnyValue()) return ValidateOptionsResult.Success;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey)) missing.Add(nameof(options.ApiKey));
        if (string.IsNullOrWhiteSpace(options.Domain)) missing.Add(nameof(options.Domain));
        if (string.IsNullOrWhiteSpace(options.From)) missing.Add(nameof(options.From));
        if (string.IsNullOrWhiteSpace(options.SignInUrl)) missing.Add(nameof(options.SignInUrl));
        if (missing.Count > 0)
            return ValidateOptionsResult.Fail(
                $"{MailgunOptions.SectionName} is partially configured; missing: {string.Join(", ", missing)}.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail($"{MailgunOptions.SectionName}:BaseUrl must be an absolute URL.");
        if (!Uri.TryCreate(options.SignInUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail($"{MailgunOptions.SectionName}:SignInUrl must be an absolute URL.");

        return ValidateOptionsResult.Success;
    }
}

static class MailgunOptionsExtensions
{
    /// <summary>
    /// Whether anyone is trying to use Mailgun at all.
    /// </summary>
    /// <remarks>
    /// Deliberately ignores SignInUrl. A deployment template can reasonably hard-code the
    /// landing URL - it is derived from the domain, not a credential - and counting it here
    /// would make an otherwise-unconfigured stack fail startup as "partially configured".
    /// </remarks>
    internal static bool HasAnyValue(this MailgunOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey)
        || !string.IsNullOrWhiteSpace(options.Domain)
        || !string.IsNullOrWhiteSpace(options.From);
}
