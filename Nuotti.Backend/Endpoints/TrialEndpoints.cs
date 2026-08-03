using Nuotti.Backend.Trials;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// Public waitlist for the exclusive event-band trial. Deliberately unauthenticated: the marketing
/// site collects interest before a performer account exists.
/// </summary>
public static class TrialEndpoints
{
    public sealed record TrialApplicationResponse(string Id, string Status, DateTimeOffset SubmittedAtUtc);

    public static void MapTrialEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/trial/applications", async (
            TrialApplicationRequest request,
            ITrialApplicationStore store,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            try
            {
                var saved = await store.SubmitAsync(request, ct);
                loggers.CreateLogger("Nuotti.Trial")
                    .LogInformation(
                        "Trial application received id={Id} band={Band} city={City} audience={Audience}",
                        saved.Id,
                        saved.BandName,
                        saved.City,
                        saved.AudienceSize);
                return Results.Ok(new TrialApplicationResponse(saved.Id, "received", saved.SubmittedAtUtc));
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["request"] = [ex.Message] });
            }
        })
        .RequireRateLimiting("trial-apply")
        .RequireCors("NuottiCors");

        // Local inspection only — Production operators read application logs / durable store instead.
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/v1/trial/applications", async (ITrialApplicationStore store, CancellationToken ct) =>
                Results.Ok(await store.ListAsync(ct)))
            .RequireCors("NuottiCors");
        }
    }
}
