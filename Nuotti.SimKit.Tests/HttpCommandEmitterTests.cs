using System.Net;
using FluentAssertions;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.SimKit.Hub;
using Xunit;

namespace Nuotti.SimKit.Tests;

public class HttpCommandEmitterTests
{
    sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    static StartGame AStartGame() => new()
    {
        SessionCode = "dev",
        IssuedByRole = Role.Performer,
        IssuedById = "perf-1"
    };

    [Fact]
    public async Task Posts_to_the_phase_route_for_the_command_type()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        await emitter.EmitAsync(AStartGame());

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be("/v1/message/phase/start-game/dev");
    }

    [Fact]
    public async Task Serialises_the_command_with_rest_camel_case_options()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        await emitter.EmitAsync(AStartGame());

        handler.LastBody.Should().Contain("\"sessionCode\":\"dev\"");
    }

    [Fact]
    public async Task Treats_accepted_as_success()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Throws_with_the_response_body_when_the_command_is_rejected()
    {
        var handler = new StubHandler(HttpStatusCode.Forbidden, "{\"reasonCode\":\"UnauthorizedRole\"}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();
        thrown.Which.ResponseBody.Should().Contain("UnauthorizedRole");
    }

    [Fact]
    public async Task Rejects_a_command_type_with_no_phase_route()
    {
        var handler = new StubHandler(HttpStatusCode.Accepted);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(new SubmitAnswer(null, 0)
        {
            SessionCode = "dev",
            IssuedByRole = Role.Audience,
            IssuedById = "aud-1"
        });

        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
