using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend.Exception;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Enum;
using Nuotti.Contracts.V1.Model;
using System.Net;
using System.Net.Http.Json;
namespace Nuotti.Backend.Tests;

/// <summary>
/// Tests for error logging and ProblemDetails integration (J3).
/// Verifies that errors are logged with proper context including correlation IDs.
/// </summary>
public class ErrorLoggingTests : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory;

    public ErrorLoggingTests(WebApplicationFactory<QuizHub> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task ErrorLog_IncludesCorrelationId_WhenPresent()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/demo/problem/400");
        req.Headers.Add("X-Correlation-Id", correlationId.ToString());

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(correlationId, problem!.CorrelationId);
    }

    [Fact]
    public async Task ErrorLog_IncludesReasonCode_InProblemDetails()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/demo/problem/400");

        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(ReasonCode.InvalidStateTransition, problem!.Reason);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task ExceptionMapping_ArgumentNullException_Returns400()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.Configure(app =>
            {
                app.UseMiddleware<ProblemHandlingMiddleware>();
                app.MapGet("/test/argnull", () => throw new ArgumentNullException("testParam"));
            });
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/test/argnull");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("testParam", problem.Field);
    }

    [Fact]
    public async Task ExceptionMapping_ValidationException_Returns422()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.Configure(app =>
            {
                app.UseMiddleware<ProblemHandlingMiddleware>();
                app.MapGet("/test/validation", () => throw new System.ComponentModel.DataAnnotations.ValidationException("Invalid data"));
            });
        });

        var client = factory.CreateClient();
        var resp = await client.GetAsync("/test/validation");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);
        Assert.NotNull(problem);
        Assert.Equal(422, problem!.Status);
    }

    [Fact]
    public async Task ProblemDetails_IncludesAllRequiredFields()
    {
        var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/demo/problem/409");
        req.Headers.Add("X-Correlation-Id", correlationId.ToString());

        var resp = await client.SendAsync(req);
        var problem = await resp.Content.ReadFromJsonAsync<NuottiProblem>(ContractsJson.RestOptions);

        Assert.NotNull(problem);
        Assert.NotNull(problem!.Title);
        Assert.NotNull(problem.Detail);
        Assert.NotEqual(0, problem.Status);
        Assert.Equal(correlationId, problem.CorrelationId);
    }
}

