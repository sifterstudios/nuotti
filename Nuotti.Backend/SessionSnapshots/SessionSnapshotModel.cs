using Nuotti.Backend.SongPackages;

namespace Nuotti.Backend.SessionSnapshots;

public sealed record ScoringPolicySnapshot(string PolicyId, int Version, int CorrectPoints,
    int SpeedBonusPoints, long SpeedBonusWindowMs);
public sealed record SessionSetlistSelection(string PackageRevisionId, string? LyricTrackRevisionId = null);
public sealed record CreateSessionSetlistSnapshotRequest(IReadOnlyList<SessionSetlistSelection> Songs,
    ScoringPolicyReference ScoringPolicy, IReadOnlyList<string> AcceptedWarningCodes);
public sealed record SnapshotAsset(string RevisionId, string AssetType, string Sha256, long Size, bool Required);
public sealed record CapturedLyricTrack(string TrackRevisionId, int Version, string Sha256, string Lrc, long OffsetMs);
public sealed record SessionSetlistItem(int Position, string CatalogEntryId, string PackageRevisionId,
    int PackageRevisionNumber, PlaybackConfiguration Playback, IReadOnlyList<PackageHint> Hints,
    CapturedLyricTrack? LyricTrack);
public sealed record SessionSetlistSnapshot(string SnapshotId, string WorkspaceId, string SessionCode,
    int Version, IReadOnlyList<SessionSetlistItem> Songs, ScoringPolicySnapshot ScoringPolicy,
    IReadOnlyList<SnapshotAsset> Assets, IReadOnlyList<string> AcceptedWarningCodes,
    string CreatedBy, DateTimeOffset CreatedAt);
public sealed record SnapshotPreflightFinding(string Code, ReadinessSeverity Severity, string Title,
    string Consequence, string Action, bool CanOverride);
public sealed record SessionSnapshotPreflight(bool CanCreate, IReadOnlyList<SnapshotPreflightFinding> Findings,
    IReadOnlyList<SnapshotAsset> Assets);

public interface ISessionSetlistSnapshotStore
{
    Task<SessionSetlistSnapshot?> GetAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default);
    Task<SessionSetlistSnapshot> CreateAsync(string workspaceId, string sessionCode,
        IReadOnlyList<SessionSetlistItem> songs, ScoringPolicySnapshot scoringPolicy,
        IReadOnlyList<SnapshotAsset> assets, IReadOnlyList<string> acceptedWarnings, string userId,
        CancellationToken cancellationToken = default);
}
