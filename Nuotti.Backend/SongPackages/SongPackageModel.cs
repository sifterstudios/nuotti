using System.Text.Json.Serialization;

namespace Nuotti.Backend.SongPackages;

[JsonConverter(typeof(JsonStringEnumConverter<PlaybackMode>))]
public enum PlaybackMode { LiveOnly, ClickOnly, BackingOnly, BackingWithClick }
[JsonConverter(typeof(JsonStringEnumConverter<PackageHintType>))]
public enum PackageHintType { Text, Image, Visual, LiveBand }
[JsonConverter(typeof(JsonStringEnumConverter<ReadinessSeverity>))]
public enum ReadinessSeverity { Ready, Warning, Blocking }

public sealed record PlaybackConfiguration(
    PlaybackMode Mode, string? BackingAssetRevisionId, string? ClickAssetRevisionId,
    long SongStartOffsetMs, long? MasterDurationMs, long? BackingDurationMs, long? ClickDurationMs,
    IReadOnlyList<int> BackingOutputChannels, IReadOnlyList<int> ClickOutputChannels);

public sealed record PackageHint(string HintId, PackageHintType Type, string? Text,
    string? AssetRevisionId, string? PerformerCue);

public sealed record LyricTrackDraft(string Lrc, long OffsetMs);

public sealed record SongPackageDocument(PlaybackConfiguration Playback, IReadOnlyList<PackageHint> Hints,
    LyricTrackDraft? Lyrics);

public sealed record SongPackageDraft(string WorkspaceId, string CatalogEntryId, SongPackageDocument Document,
    string UpdatedBy, DateTimeOffset UpdatedAt);

public sealed record SongPackageRevision(string WorkspaceId, string CatalogEntryId, string RevisionId,
    int RevisionNumber, SongPackageDocument Document, string RevisionNote, string PublishedBy,
    DateTimeOffset PublishedAt, IReadOnlyList<string> AcceptedWarningCodes);

public sealed record ReadinessFinding(string Code, ReadinessSeverity Severity, string Section,
    string Title, string Consequence, string RecommendedAction, bool CanOverride);

public sealed record SongPackageReadiness(bool CanPublish, IReadOnlyList<ReadinessFinding> Findings,
    ProjectorPackagePreview Preview);

public sealed record ProjectorPackagePreview(IReadOnlyList<ProjectorHintPreview> Hints,
    IReadOnlyList<ProjectorLyricLine> Lyrics, long? MasterDurationMs);
public sealed record ProjectorHintPreview(int Order, PackageHintType Type, string DisplayText, string? AssetRevisionId);
public sealed record ProjectorLyricLine(long ActivationMs, string Text);

public interface ISongPackageStore
{
    Task<SongPackageDraft> SaveDraftAsync(string workspaceId, string catalogEntryId, SongPackageDocument document,
        string userId, CancellationToken cancellationToken = default);
    Task<SongPackageDraft?> GetDraftAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default);
    Task<SongPackageRevision> PublishAsync(string workspaceId, string catalogEntryId, SongPackageDocument document,
        string revisionNote, IReadOnlyList<string> acceptedWarningCodes, string userId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongPackageRevision>> GetRevisionsAsync(string workspaceId, string catalogEntryId,
        CancellationToken cancellationToken = default);
    Task<SongPackageRevision?> GetRevisionAsync(string workspaceId, string revisionId,
        CancellationToken cancellationToken = default);
}
