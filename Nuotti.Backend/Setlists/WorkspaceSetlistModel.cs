namespace Nuotti.Backend.Setlists;

/// <summary>One slot in a Workspace Setlist — pins an immutable Song Package Revision.</summary>
public sealed record SetlistSongSelection(string PackageRevisionId, string? LyricTrackRevisionId = null);

public sealed record WorkspaceSetlist(
    string WorkspaceId,
    IReadOnlyList<SetlistSongSelection> Songs,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

public sealed record SaveWorkspaceSetlistRequest(IReadOnlyList<SetlistSongSelection> Songs);

/// <summary>Catalog song with its latest published package revision — for Setlist picking.</summary>
public sealed record PublishedLibrarySong(
    string CatalogEntryId,
    string Title,
    string Artist,
    string PackageRevisionId,
    int RevisionNumber,
    DateTimeOffset PublishedAt);

public interface IWorkspaceSetlistStore
{
    Task<WorkspaceSetlist?> GetAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceSetlist> SaveAsync(string workspaceId, IReadOnlyList<SetlistSongSelection> songs, string userId,
        CancellationToken cancellationToken = default);
}
