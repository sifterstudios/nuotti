using Nuotti.Backend.Assets;
using Nuotti.Backend.Exception;
using Nuotti.Backend.Setlists;
using Nuotti.Backend.SongPackages;
using Nuotti.Backend.Workspaces;
using Nuotti.Contracts.V1.Enum;

namespace Nuotti.Backend.Endpoints;

public static class WorkspaceSetlistEndpoints
{
    public static void MapWorkspaceSetlistEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/workspaces/{workspaceId}/library/published", async (
            HttpContext http, string workspaceId,
            IWorkspaceAccessStore access, IPrivateAssetMetadataStore catalog, ISongPackageStore packages,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();

            var entries = await catalog.ListEntriesAsync(workspaceId, ct);
            var published = new List<PublishedLibrarySong>();
            foreach (var entry in entries)
            {
                var revisions = await packages.GetRevisionsAsync(workspaceId, entry.CatalogEntryId, ct);
                var latest = revisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
                if (latest is null) continue;
                published.Add(new PublishedLibrarySong(
                    entry.CatalogEntryId, entry.Title, entry.Artist,
                    latest.RevisionId, latest.RevisionNumber, latest.PublishedAt));
            }

            return Results.Ok(published
                .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Artist, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        });

        app.MapGet("/v1/workspaces/{workspaceId}/setlist", async (
            HttpContext http, string workspaceId,
            IWorkspaceAccessStore access, IWorkspaceSetlistStore setlists, CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var setlist = await setlists.GetAsync(workspaceId, ct);
            return Results.Ok(setlist ?? new WorkspaceSetlist(workspaceId, [], DateTimeOffset.MinValue, ""));
        });

        app.MapPut("/v1/workspaces/{workspaceId}/setlist", async (
            HttpContext http, string workspaceId, SaveWorkspaceSetlistRequest request,
            IWorkspaceAccessStore access, IWorkspaceSetlistStore setlists, ISongPackageStore packages,
            CancellationToken ct) =>
        {
            var selected = await WorkspaceHttpAccess.RequireSelectedAsync(http, access, workspaceId, ct);
            if (selected.Principal is null) return Results.Unauthorized();
            if (selected.Access is null) return Results.NotFound();
            var songs = request.Songs ?? [];
            if (songs.Count > 100)
                return ProblemResults.BadRequest("Setlist is too long.",
                    "A setlist may contain at most 100 songs.", ReasonCode.InvalidStateTransition);
            if (songs.Any(x => string.IsNullOrWhiteSpace(x.PackageRevisionId)))
                return ProblemResults.BadRequest("Setlist songs are invalid.",
                    "Every song must reference a published package revision.", ReasonCode.InvalidStateTransition);

            foreach (var song in songs)
            {
                var revision = await packages.GetRevisionAsync(workspaceId, song.PackageRevisionId.Trim(), ct);
                if (revision is null)
                    return ProblemResults.BadRequest("Unknown package revision.",
                        $"Revision '{song.PackageRevisionId}' is not published in this workspace.",
                        ReasonCode.InvalidStateTransition, "songs");
            }

            var normalized = songs
                .Select(x => new SetlistSongSelection(x.PackageRevisionId.Trim(),
                    string.IsNullOrWhiteSpace(x.LyricTrackRevisionId) ? null : x.LyricTrackRevisionId.Trim()))
                .ToArray();
            return Results.Ok(await setlists.SaveAsync(workspaceId, normalized, selected.Principal.UserId, ct));
        });
    }
}
