using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend.Exception;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using System.Net.Http.Json;
using System.Text.Json;
namespace Nuotti.Backend.Tests;

public class ProblemDetailsTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { });

    [Theory]
    [InlineData("400", 400, ReasonCode.InvalidStateTransition, "name", "Invalid input")]
    [InlineData("409", 409, ReasonCode.DuplicateCommand, null, "Duplicate command")]
    [InlineData("422", 422, ReasonCode.UnauthorizedRole, "issuedByRole", "Business rule violated")]
    [InlineData("badrequest", 400, ReasonCode.InvalidStateTransition, "name", "Invalid input")]
    [InlineData("conflict", 409, ReasonCode.DuplicateCommand, null, "Duplicate command")]
    [InlineData("unprocessable", 422, ReasonCode.UnauthorizedRole, "issuedByRole", "Business rule violated")]
    public async Task Demo_problem_endpoints_return_expected_shape_and_reason(string kind, int expectedStatus, ReasonCode expectedReason, string? expectedField, string expectedTitle)
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/demo/problem/{kind}");
        Assert.Equal(expectedStatus, (int)resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<NuottiProblem>(json, ContractsJson.RestOptions)!;

        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedReason, problem.Reason);
        Assert.Equal(expectedField, problem.Field);
    }

    [Fact]
    public async Task CorrelationId_is_echoed_when_present_on_problem_response()
    {
        var client = _factory.CreateClient();
        var correlation = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/demo/problem/400");
        req.Headers.Add("X-Correlation-Id", correlation.ToString());
        var resp = await client.SendAsync(req);

        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.Equal(correlation, problem!.CorrelationId);
    }

    [Theory]
    [InlineData("arg", 400, ReasonCode.InvalidStateTransition)]
    [InlineData("unauth", 403, ReasonCode.UnauthorizedRole)]
    [InlineData("invalidop", 409, ReasonCode.DuplicateCommand)]
    [InlineData("unknown", 500, ReasonCode.None)]
    public async Task Middleware_maps_exceptions_to_expected_reason_and_status(string mode, int expectedStatus, ReasonCode expectedReason)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.Configure(app =>
            {
                app.UseMiddleware<ProblemHandlingMiddleware>();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/test/throw/{mode}", context =>
                    {
                        var modeVal = (string?)context.Request.RouteValues["mode"] ?? string.Empty;
                        if (modeVal == "arg") throw new ArgumentException("bad value", nameof(mode));
                        if (modeVal == "unauth") throw new UnauthorizedAccessException("nope");
                        if (modeVal == "invalidop") throw new InvalidOperationException("dup");
                        throw new System.Exception("boom");
                    });
                });
            });
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync($"/test/throw/{mode}");
        Assert.Equal(expectedStatus, (int)resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.Equal(expectedReason, problem!.Reason);
        Assert.Equal(expectedStatus, problem.Status);
    }
}
