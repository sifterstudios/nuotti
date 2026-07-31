using Nuotti.Backend.Assets;
using Nuotti.Backend.SongPackages;

namespace Nuotti.Backend.SessionSnapshots;

public sealed class SessionSnapshotBuilder(ISongPackageStore packages, IPrivateAssetMetadataStore assets,
    ILyricTrackRevisionStore lyrics, IScoringPolicyCatalog scoringPolicies)
{
    public async Task<(IReadOnlyList<SessionSetlistItem> Songs, ScoringPolicySnapshot? Policy,
        SessionSnapshotPreflight Preflight)> BuildAsync(
        string workspaceId, IReadOnlyList<SessionSetlistSelection> selections, ScoringPolicyReference policyReference,
        IReadOnlySet<string> acceptedWarnings, CancellationToken ct = default)
    {
        var findings = new List<SnapshotPreflightFinding>();
        var songs = new List<SessionSetlistItem>();
        var manifest = new Dictionary<string, SnapshotAsset>(StringComparer.Ordinal);
        if (selections is not { Count: > 0 and <= 200 })
            Block(findings, "setlist.invalid", "Setlist must contain between 1 and 200 exact Song Package Revisions.",
                "The Session cannot determine its Rounds.", "Select at least one published revision.");
        var policy = policyReference is null ? null : scoringPolicies.Resolve(policyReference);
        if (policy is null)
            Block(findings, "scoring.invalid", "Scoring Policy is invalid.",
                "Audience scores would not be reproducible.", "Select a versioned non-negative Scoring Policy.");

        for (var index = 0; index < Math.Min(selections?.Count ?? 0, 200); index++)
        {
            var revisionId = selections![index].PackageRevisionId?.Trim();
            var revision = string.IsNullOrWhiteSpace(revisionId) ? null
                : await packages.GetRevisionAsync(workspaceId, revisionId, ct);
            if (revision is null)
            {
                Block(findings, $"song.{index + 1}.revision-missing", $"Song {index + 1} revision is unavailable.",
                    "The Session would not capture the selected immutable package.", "Select an existing Workspace revision.");
                continue;
            }
            var requestedLyricRevision = !string.IsNullOrWhiteSpace(selections[index].LyricTrackRevisionId);
            var lyricRevision = !requestedLyricRevision
                ? await lyrics.GetCurrentAsync(workspaceId, revision.CatalogEntryId, ct)
                : await lyrics.GetAsync(workspaceId, selections[index].LyricTrackRevisionId!, ct);
            if (requestedLyricRevision && lyricRevision is null)
                Block(findings, $"song.{index + 1}.lyrics-revision-missing",
                    $"Song {index + 1} selected Lyric Track Revision is unavailable.",
                    "The Session cannot capture the exact selected lyrics.", "Select an existing Lyric Track Revision.");
            if (lyricRevision is not null && lyricRevision.CatalogEntryId != revision.CatalogEntryId)
            {
                Block(findings, $"song.{index + 1}.lyrics-wrong-song", $"Song {index + 1} Lyric Track belongs to another song.",
                    "The Projector would show incorrect lyrics.", "Select a Lyric Track Revision for this Song Package.");
                lyricRevision = null;
            }
            var lyric = lyricRevision is null ? null : new CapturedLyricTrack(lyricRevision.RevisionId,
                lyricRevision.Version, lyricRevision.Sha256, lyricRevision.Lrc, lyricRevision.OffsetMs);
            songs.Add(new(index + 1, revision.CatalogEntryId, revision.RevisionId, revision.RevisionNumber,
                revision.Document.Playback, revision.Document.Hints.ToArray(), lyric));
            await AddAssetsAsync(workspaceId, index + 1, revision.Document, manifest, findings, ct);
            if (lyricRevision is null && !requestedLyricRevision)
                Warn(findings, $"song.{index + 1}.lyrics-missing", $"Song {index + 1} has no Lyric Track.",
                    "The Projector will use the reveal state during playback.", "Accept lyric-free playback for this Session.");
        }
        if (songs.Count == selections?.Count && songs.Count > 0)
            Ready(findings, "setlist.exact-revisions", $"{songs.Count} exact Song Package Revision{(songs.Count == 1 ? "" : "s")} captured.");
        var warningsAccepted = findings.Where(x => x.Severity == ReadinessSeverity.Warning)
            .All(x => acceptedWarnings.Contains(x.Code));
        return (songs, policy, new(findings.All(x => x.Severity != ReadinessSeverity.Blocking) && warningsAccepted,
            findings, manifest.Values.OrderBy(x => x.RevisionId, StringComparer.Ordinal).ToArray()));
    }

    async Task AddAssetsAsync(string workspaceId, int position, SongPackageDocument document,
        Dictionary<string, SnapshotAsset> manifest, List<SnapshotPreflightFinding> findings, CancellationToken ct)
    {
        var required = new[] { document.Playback.BackingAssetRevisionId, document.Playback.ClickAssetRevisionId }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToHashSet(StringComparer.Ordinal);
        var visual = document.Hints.Where(x => x.Type is PackageHintType.Image or PackageHintType.Visual)
            .Select(x => x.AssetRevisionId).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
        var hasNonVisualHint = document.Hints.Any(x => x.Type == PackageHintType.Text && !string.IsNullOrWhiteSpace(x.Text)
            || x.Type == PackageHintType.LiveBand && !string.IsNullOrWhiteSpace(x.PerformerCue));
        foreach (var revisionId in required.Concat(visual).Distinct(StringComparer.Ordinal))
        {
            var asset = await assets.GetAsync(workspaceId, revisionId, ct);
            var isRequired = required.Contains(revisionId) || visual.Contains(revisionId) && !hasNonVisualHint;
            var usable = asset?.Status == AssetRevisionStatus.Published && !string.IsNullOrWhiteSpace(asset.Sha256)
                && asset.StoredSize is > 0 && (asset.Provenance.RightsExpiresAt is null
                    || asset.Provenance.RightsExpiresAt > DateTimeOffset.UtcNow);
            if (usable)
            {
                var requiredByAnySong = isRequired || manifest.GetValueOrDefault(revisionId)?.Required == true;
                manifest[revisionId] = new(revisionId, asset!.AssetType, asset.Sha256!, asset.StoredSize!.Value,
                    requiredByAnySong);
            }
            else if (required.Contains(revisionId))
                Block(findings, $"song.{position}.asset.{revisionId}.unavailable", $"Song {position} required audio is unavailable.",
                    "The Show Agent cannot cache verified playback.", "Restore current rights or publish a replacement package revision.");
        }
        var hasFallback = hasNonVisualHint || visual.Any(id => manifest.ContainsKey(id));
        foreach (var revisionId in visual.Where(id => !manifest.ContainsKey(id)).Distinct(StringComparer.Ordinal))
            if (hasFallback)
                Warn(findings, $"song.{position}.visual.{revisionId}.unavailable", $"Song {position} visual media is unavailable.",
                    "That Hint cannot be rendered at the venue.", "Accept the missing visual because another usable Hint remains.");
            else
                Block(findings, $"song.{position}.visual.{revisionId}.required", $"Song {position} has no usable Hint at the venue.",
                    "The Audience would receive no fair clue.", "Restore the visual or publish a package with another usable Hint.");
    }

    static void Ready(List<SnapshotPreflightFinding> target, string code, string title) =>
        target.Add(new(code, ReadinessSeverity.Ready, title, "", "", false));
    static void Warn(List<SnapshotPreflightFinding> target, string code, string title, string consequence, string action) =>
        target.Add(new(code, ReadinessSeverity.Warning, title, consequence, action, true));
    static void Block(List<SnapshotPreflightFinding> target, string code, string title, string consequence, string action) =>
        target.Add(new(code, ReadinessSeverity.Blocking, title, consequence, action, false));
}
