using Microsoft.AspNetCore.Mvc.Testing;
using Nuotti.Backend.Endpoints;
using Nuotti.Backend.Trials;
using System.Net;
using System.Net.Http.Json;

namespace Nuotti.Backend.Tests;

public class TrialEndpointsTests(WebApplicationFactory<QuizHub> factory) : IClassFixture<WebApplicationFactory<QuizHub>>
{
    readonly WebApplicationFactory<QuizHub> _factory = factory.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task Submit_ValidApplication_ReturnsReceived()
    {
        using var client = _factory.CreateClient();
        var email = $"band-{Guid.NewGuid():N}@example.com";

        var resp = await client.PostAsJsonAsync("/v1/trial/applications", new TrialApplicationRequest(
            BandName: "Neon Parade",
            ContactName: "Alex Rivera",
            Email: email,
            City: "Helsinki",
            AudienceSize: "150-400",
            Note: "Weekend club residencies"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<TrialEndpoints.TrialApplicationResponse>();
        Assert.NotNull(body);
        Assert.Equal("received", body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.Id));
    }

    [Fact]
    public async Task Submit_SameEmail_UpdatesInsteadOfDuplicating()
    {
        using var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/v1/trial/applications", new TrialApplicationRequest(
            "First Band", "Sam", email, "Tampere", "under-50"));
        var firstBody = await first.Content.ReadFromJsonAsync<TrialEndpoints.TrialApplicationResponse>();

        var second = await client.PostAsJsonAsync("/v1/trial/applications", new TrialApplicationRequest(
            "Second Band", "Sam", email, "Turku", "50-150"));
        var secondBody = await second.Content.ReadFromJsonAsync<TrialEndpoints.TrialApplicationResponse>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody!.Id, secondBody!.Id);

        var listed = await client.GetFromJsonAsync<List<TrialApplication>>("/v1/trial/applications");
        Assert.NotNull(listed);
        var match = Assert.Single(listed, a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Second Band", match.BandName);
    }

    [Fact]
    public async Task Submit_InvalidEmail_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/v1/trial/applications", new TrialApplicationRequest(
            "Band", "Name", "not-an-email", "Oslo", "50-150"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Submit_UnknownAudienceSize_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/v1/trial/applications", new TrialApplicationRequest(
            "Band", "Name", "ok@example.com", "Oslo", "huge"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
