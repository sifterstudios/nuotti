using ServiceDefaults;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// About endpoints exposing version and build information.
/// </summary>
internal static class AboutEndpoints
{
    public static void MapAboutEndpoints(this WebApplication app)
    {
        app.MapGet("/about", () =>
        {
            var info = VersionInfo.GetVersionInfo("Nuotti.Backend");
            return Results.Json(info, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            },
            contentType: "application/json");
        })
        .RequireCors("NuottiCors");
    }
}

