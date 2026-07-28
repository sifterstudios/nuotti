using System.Net;
using System.Text.Json;
using FluentAssertions;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Message.Phase;
using Nuotti.Contracts.V1.Model;
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
        thrown.Which.RawPayload.Should().Contain("UnauthorizedRole");
    }

    [Fact]
    public async Task Rejection_carries_the_structured_reason_from_a_real_problem_document()
    {
        // Unlike the test above (an arbitrary body a caller might string-match), this stubs the
        // shape the Backend actually sends: a NuottiProblem serialized with the same camelCase
        // REST options PhaseEndpoints/ProblemResults use. Deserializing it into Problem is what
        // lets a caller ask "was this rejected for UnauthorizedRole?" the same way it would for
        // InProcCommandEmitter (see InProcCommandEmitterTests in Nuotti.SimKit.InProc.Tests),
        // instead of only being able to string-match RawPayload.
        var problem = new NuottiProblem(
            Title: "Unauthorized Role",
            Status: 403,
            Detail: "Only Performer may execute this command.",
            Reason: ReasonCode.UnauthorizedRole,
            Field: "issuedByRole");
        var body = JsonSerializer.Serialize(problem, ContractsJson.RestOptions);
        var handler = new StubHandler(HttpStatusCode.Forbidden, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();
        thrown.Which.Problem.Should().NotBeNull();
        thrown.Which.Problem!.Reason.Should().Be(ReasonCode.UnauthorizedRole);
    }

    [Theory]
    [InlineData("<html>502 Bad Gateway</html>")]
    // JsonSerializer.Deserialize<NuottiProblem> only throws for non-JSON input. Any JSON
    // *object* - including these two - binds through NuottiProblem's positional constructor
    // with missing fields silently defaulted, so without the Status-based guard these would
    // produce a non-null Problem with a fabricated Reason of None and Status of 0. The first
    // is this file's own earlier test body (Throws_with_the_response_body_when_the_command_is_
    // rejected uses the same shape, but only ever asserted on RawPayload, not Problem - which
    // is exactly how this bug went unnoticed).
    [InlineData("{\"reasonCode\":\"UnauthorizedRole\"}")]
    [InlineData("{}")]
    [InlineData("{\"error\":\"upstream timeout\"}")]
    public async Task A_rejection_body_that_is_not_a_genuine_NuottiProblem_leaves_Problem_null(string body)
    {
        var handler = new StubHandler(HttpStatusCode.BadGateway, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5240") };
        var emitter = new HttpCommandEmitter(http);

        var act = async () => await emitter.EmitAsync(AStartGame());

        var thrown = await act.Should().ThrowAsync<CommandRejectedException>();
        thrown.Which.Problem.Should().BeNull();
        thrown.Which.RawPayload.Should().Be(body);
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
