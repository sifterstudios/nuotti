using ServiceDefaults;
using System.Text.Json;
namespace Nuotti.Performer.Endpoints;

/// <summary>
/// About endpoints exposing version and build information.
/// </summary>
internal static class AboutEndpoints
{
    public static void MapAboutEndpoints(this WebApplication app)
    {
        app.MapGet("/about", () =>
        {
            var info = VersionInfo.GetVersionInfo("Nuotti.Performer");
            return Results.Json(info, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            },
            contentType: "application/json");
        });
    }
}




