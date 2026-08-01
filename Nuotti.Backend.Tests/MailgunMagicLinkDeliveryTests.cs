using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuotti.Backend.Workspaces;
using System.Net;
using System.Text;

namespace Nuotti.Backend.Tests;

/// <summary>
/// Mailgun rejects a request that is authenticated wrongly, encoded wrongly, or sent to the
/// wrong region, and every one of those failures looks the same from the outside: 401. These
/// tests pin the wire format so a regression is caught here rather than by a user who cannot
/// sign in.
/// </summary>
public sealed class MailgunMagicLinkDeliveryTests
{
    static MailgunOptions ValidOptions(string? baseUrl = null) => new()
    {
        ApiKey = "key-secret",
        Domain = "mg.nuotti.app",
        From = "Nuotti <no-reply@nuotti.app>",
        SignInUrl = "https://performer.nuotti.app/signin",
        BaseUrl = baseUrl ?? "https://api.eu.mailgun.net"
    };

    static IssuedMagicLink Link(string token = "tok-123") =>
        new(token, DateTimeOffset.UtcNow.AddMinutes(15));

    static MailgunMagicLinkDelivery Create(MailgunOptions options, RecordingHandler handler) =>
        new(new StubHttpClientFactory(handler), Options.Create(options), NullLogger<MailgunMagicLinkDelivery>.Instance);

    [Fact]
    public async Task Posts_to_the_regional_messages_endpoint_for_the_configured_domain()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        var delivered = await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.True(delivered);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "https://api.eu.mailgun.net/v3/mg.nuotti.app/messages",
            handler.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Honours_a_configured_region_rather_than_hard_coding_one()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Create(ValidOptions("https://api.mailgun.net/"), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.Equal(
            "https://api.mailgun.net/v3/mg.nuotti.app/messages",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Authenticates_as_basic_api_and_the_key()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        var authorization = handler.Request!.Headers.Authorization;
        Assert.Equal("Basic", authorization!.Scheme);
        Assert.Equal(
            "api:key-secret",
            Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter!)));
    }

    [Fact]
    public async Task Sends_form_encoded_fields_because_the_api_rejects_json()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.Equal("application/x-www-form-urlencoded", handler.Request!.Content!.Headers.ContentType!.MediaType);
        var form = handler.Form();
        Assert.Equal("Nuotti <no-reply@nuotti.app>", form["from"]);
        Assert.Equal("band@example.com", form["to"]);
        Assert.False(string.IsNullOrWhiteSpace(form["subject"]));
        Assert.False(string.IsNullOrWhiteSpace(form["text"]));
        Assert.False(string.IsNullOrWhiteSpace(form["html"]));
    }

    [Fact]
    public async Task Embeds_the_token_as_a_query_parameter_on_the_configured_sign_in_url()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link("abc.def"), MagicLinkPurpose.SignIn);

        Assert.Contains("https://performer.nuotti.app/signin?token=abc.def", handler.Form()["text"]);
    }

    [Fact]
    public void Escapes_a_token_that_would_otherwise_break_the_query_string()
    {
        var url = MailgunMagicLinkDelivery.BuildSignInUrl("https://performer.nuotti.app/signin", "a+b&c=d");

        Assert.Equal("https://performer.nuotti.app/signin?token=a%2bb%26c%3dd", url);
    }

    [Fact]
    public void Sign_in_and_invitation_do_not_send_the_same_email()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);

        var signIn = MailgunMagicLinkDelivery.Compose("Nuotti", MagicLinkPurpose.SignIn, "https://x/signin?token=t", expires);
        var invite = MailgunMagicLinkDelivery.Compose("Nuotti", MagicLinkPurpose.Invitation, "https://x/signin?token=t", expires);

        Assert.NotEqual(signIn.Subject, invite.Subject);
        Assert.NotEqual(signIn.Text, invite.Text);
        Assert.Contains("invited", invite.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reports_failure_rather_than_claiming_a_send_when_mailgun_rejects_it()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, "Forbidden");

        var delivered = await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.False(delivered);
    }

    [Fact]
    public async Task Refuses_to_send_when_the_section_is_incomplete()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var incomplete = new MailgunOptions { ApiKey = "key-secret", Domain = "mg.nuotti.app" };

        var delivered = await Create(incomplete, handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.False(delivered);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Surfaces_a_transport_failure_as_a_failed_delivery()
    {
        var handler = new RecordingHandler(new HttpRequestException("no route to host"));

        var delivered = await Create(ValidOptions(), handler)
            .DeliverAsync("band@example.com", Link(), MagicLinkPurpose.SignIn);

        Assert.False(delivered);
    }

    [Theory]
    [InlineData("ApiKey")]
    [InlineData("Domain")]
    [InlineData("From")]
    [InlineData("SignInUrl")]
    public void Validation_rejects_a_half_configured_section(string omitted)
    {
        var options = new MailgunOptions
        {
            ApiKey = omitted == "ApiKey" ? null : "key-secret",
            Domain = omitted == "Domain" ? null : "mg.nuotti.app",
            From = omitted == "From" ? null : "Nuotti <no-reply@nuotti.app>",
            SignInUrl = omitted == "SignInUrl" ? null : "https://performer.nuotti.app/signin"
        };

        var result = new MailgunOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(omitted, result.FailureMessage);
    }

    [Fact]
    public void Validation_accepts_an_entirely_absent_section()
    {
        // Not configuring Mailgun is a legitimate choice - it means the webhook adapter is in use.
        var result = new MailgunOptionsValidator().Validate(null, new MailgunOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_templated_sign_in_url_alone_does_not_count_as_configuring_mailgun()
    {
        // deploy/docker-compose.unraid.yml always sets SignInUrl - it is derived from the
        // domain, not a secret. Treating that as "partially configured" would refuse to start
        // every deployment that has not signed up for Mailgun yet.
        var result = new MailgunOptionsValidator().Validate(
            null, new MailgunOptions { SignInUrl = "https://performer.nuotti.app/signin" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_sign_in_url_alone_does_not_enable_mailgun_delivery()
    {
        var options = new MailgunOptions { SignInUrl = "https://performer.nuotti.app/signin" };

        Assert.False(options.IsConfigured);
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        readonly HttpStatusCode _status;
        readonly string _body;
        readonly System.Exception? _throw;

        public RecordingHandler(HttpStatusCode status, string body = "") => (_status, _body) = (status, body);
        public RecordingHandler(System.Exception toThrow) => (_status, _body, _throw) = (HttpStatusCode.OK, "", toThrow);

        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        public Dictionary<string, string> Form() =>
            Body!.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(
                    parts => Uri.UnescapeDataString(parts[0].Replace('+', ' ')),
                    parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "");

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null) Body = await request.Content.ReadAsStringAsync(cancellationToken);
            if (_throw is not null) throw _throw;
            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
