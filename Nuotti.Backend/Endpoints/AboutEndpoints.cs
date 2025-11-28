using ServiceDefaults;

namespace Nuotti.Backend.Endpoints;

/// <summary>
/// About endpoints exposing version and build information.
/// </summary>
internal static class AboutEndpoints
{
    public static void MapAboutEndpoints(this WebApplication app)
    {
        app.MapGet("/about", (IConfiguration configuration) =>
        {
            var info = VersionInfo.GetVersionInfo("Nuotti.Backend");
            var features = FeatureFlags.GetAll(configuration);
            
            // Return extended about info with feature flags
            var aboutInfo = new
            {
                service = info.Service,
                version = info.Version,
                gitCommit = info.GitCommit,
                buildTime = info.BuildTime,
                runtime = info.Runtime,
                features = features
            };
            
            return Results.Json(aboutInfo, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            },
            contentType: "application/json");
        })
        .RequireCors("NuottiCors");
    }
}

