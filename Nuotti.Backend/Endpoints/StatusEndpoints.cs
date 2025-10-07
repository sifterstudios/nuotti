using Nuotti.Backend.Sessions;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Endpoints;

internal static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/status/{session}", (IGameStateStore store, string session) =>
        {
            if (store.TryGet(session, out GameStateSnapshot snapshot))
            {
                return Results.Ok(snapshot);
            }
            return Results.NotFound();
        }).RequireCors("NuottiCors");
    }
}
