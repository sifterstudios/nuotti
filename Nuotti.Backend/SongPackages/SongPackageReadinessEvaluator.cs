using System.Globalization;
using System.Text.RegularExpressions;
using Nuotti.Backend.Assets;

namespace Nuotti.Backend.SongPackages;

public sealed partial class SongPackageReadinessEvaluator(IPrivateAssetMetadataStore assets)
{
    public async Task<SongPackageReadiness> EvaluateAsync(string workspaceId, SongPackageDocument document,
        IReadOnlySet<string>? acceptedWarnings = null, CancellationToken cancellationToken = default)
    {
        acceptedWarnings ??= new HashSet<string>();
        var findings = new List<ReadinessFinding>();
        await ValidatePlaybackAsync(workspaceId, document.Playback, findings, cancellationToken);
        await ValidateHintsAsync(workspaceId, document.Hints, findings, cancellationToken);
        var lyricLines = ValidateLyrics(document, findings);
        var warningsAccepted = findings.Where(x => x.Severity == ReadinessSeverity.Warning)
            .All(x => x.CanOverride && acceptedWarnings.Contains(x.Code));
        var canPublish = findings.All(x => x.Severity != ReadinessSeverity.Blocking) && warningsAccepted;
        var hintPreview = document.Hints.Select((hint, index) => new ProjectorHintPreview(index + 1, hint.Type,
            hint.Type == PackageHintType.LiveBand ? "Live band hint" : hint.Text?.Trim() ?? "Visual hint",
            hint.AssetRevisionId)).ToArray();
        return new(canPublish, findings, new(hintPreview, lyricLines, document.Playback.MasterDurationMs));
    }

    async Task ValidatePlaybackAsync(string workspaceId, PlaybackConfiguration playback,
        List<ReadinessFinding> findings, CancellationToken ct)
    {
        if (playback.SongStartOffsetMs < 0)
            Block(findings, "playback.offset.invalid", "Playback", "Start offset is invalid",
                "The backing and lyrics would start before the master timeline.", "Use zero or a positive offset.");

        var needsBacking = playback.Mode is PlaybackMode.BackingOnly or PlaybackMode.BackingWithClick;
        var needsClick = playback.Mode is PlaybackMode.ClickOnly or PlaybackMode.BackingWithClick;
        if (playback.Mode == PlaybackMode.LiveOnly)
        {
            if (playback.BackingAssetRevisionId is not null || playback.ClickAssetRevisionId is not null
                || playback.BackingOutputChannels.Count > 0 || playback.ClickOutputChannels.Count > 0)
                Block(findings, "playback.live-only.has-audio", "Playback", "Live-only contains audio setup",
                    "The package has ambiguous Engine behavior.", "Remove tracks and output routing for live-only mode.");
            else Ready(findings, "playback.live-only.ready", "Playback", "Live-only playback is ready");
            return;
        }

        if (playback.MasterDurationMs is not > 0)
            Block(findings, "playback.duration.missing", "Playback", "Master duration is missing",
                "Timed playback and Projector preview cannot share one endpoint.", "Set the verified master duration.");

        var backing = needsBacking
            ? await ValidateAssetAsync(workspaceId, playback.BackingAssetRevisionId, "backing-track", "backing", findings, ct)
            : null;
        var click = needsClick
            ? await ValidateAssetAsync(workspaceId, playback.ClickAssetRevisionId, "click-track", "click", findings, ct)
            : null;

        ValidateRouting(playback, needsBacking, needsClick, findings);
        if (needsBacking && playback.BackingDurationMs is not > 0)
            Block(findings, "playback.backing.duration-missing", "Playback", "Backing duration is missing",
                "The backing cannot be placed on the master timeline.", "Probe and save the backing duration.");
        if (needsClick && playback.ClickDurationMs is not > 0)
            Block(findings, "playback.click.duration-missing", "Playback", "Click duration is missing",
                "The click cannot be placed on the master timeline.", "Probe and save the click duration.");
        if (playback.MasterDurationMs is { } master)
        {
            if (needsBacking && playback.BackingDurationMs is { } backingDuration
                && Math.Abs(backingDuration + playback.SongStartOffsetMs - master) > 10)
                Block(findings, "playback.backing.timeline-mismatch", "Playback", "Backing does not end on the master timeline",
                    "Backing and click could stop at different times.", "Correct the backing offset or use an aligned file.");
            if (needsClick && playback.ClickDurationMs is { } clickDuration && Math.Abs(clickDuration - master) > 10)
                Block(findings, "playback.click.timeline-mismatch", "Playback", "Click duration differs from the master timeline",
                    "The band click could drift from show visuals.", "Use the verified click duration as the master duration.");
        }
        if (backing is not null || click is not null)
            Ready(findings, "playback.assets.verified", "Playback", "Required audio revisions are published and hash verified");
    }

    async Task<PrivateAssetRevision?> ValidateAssetAsync(string workspaceId, string? revisionId,
        string expectedType, string label, List<ReadinessFinding> findings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(revisionId))
        {
            Block(findings, $"playback.{label}.missing", "Playback", $"Required {label} is missing",
                $"{label} playback cannot be prepared.", $"Select a published {label} asset revision.");
            return null;
        }
        var asset = await assets.GetAsync(workspaceId, revisionId, ct);
        if (asset?.Status != AssetRevisionStatus.Published || asset.AssetType != expectedType
            || string.IsNullOrWhiteSpace(asset.Sha256))
        {
            Block(findings, $"playback.{label}.unusable", "Playback", $"Required {label} is not usable",
                "The Show Agent cannot verify and prepare the required audio.",
                $"Publish a valid {expectedType} revision in this Workspace.");
            return null;
        }
        if (asset.Provenance.RightsExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            Block(findings, $"playback.{label}.rights-expired", "Playback", $"{label} usage rights have expired",
                "The asset may not be downloaded for the show.", "Renew the rights evidence or replace the asset.");
            return null;
        }
        return asset;
    }

    static void ValidateRouting(PlaybackConfiguration playback, bool needsBacking, bool needsClick,
        List<ReadinessFinding> findings)
    {
        var backingValid = !needsBacking || playback.BackingOutputChannels is { Count: 2 }
            && ValidChannels(playback.BackingOutputChannels);
        var clickValid = !needsClick || playback.ClickOutputChannels is { Count: 1 }
            && ValidChannels(playback.ClickOutputChannels);
        var overlap = playback.BackingOutputChannels.Intersect(playback.ClickOutputChannels).Any();
        if (!backingValid || !clickValid || overlap)
            Block(findings, "playback.routing.invalid", "Playback", "Output routing is impossible",
                "Signals could be missing or click could reach the audience mix.",
                "Choose two distinct backing outputs and one separate click output.");
        else Ready(findings, "playback.routing.ready", "Playback", "Output routing is valid");
    }

    async Task ValidateHintsAsync(string workspaceId, IReadOnlyList<PackageHint> hints,
        List<ReadinessFinding> findings, CancellationToken ct)
    {
        if (hints.Count == 0)
        {
            Block(findings, "hints.none", "Hints", "At least one Hint is required",
                "The Round would give the Audience nothing to guess from.", "Add a usable text, visual, or live-band Hint.");
            return;
        }
        if (hints.Count > 20 || hints.Select(x => x.HintId).Distinct(StringComparer.Ordinal).Count() != hints.Count)
            Block(findings, "hints.sequence.invalid", "Hints", "Hint sequence is invalid",
                "The Performer could reveal an ambiguous sequence.", "Use 1–20 Hints with unique identifiers.");
        var usable = 0;
        var brokenVisuals = 0;
        foreach (var hint in hints)
        {
            if (hint.Type == PackageHintType.LiveBand && Text(hint.PerformerCue, 500)) { usable++; continue; }
            if (hint.Type == PackageHintType.Text && Text(hint.Text, 500)) { usable++; continue; }
            if (hint.Type is PackageHintType.Image or PackageHintType.Visual)
            {
                var asset = string.IsNullOrWhiteSpace(hint.AssetRevisionId) ? null
                    : await assets.GetAsync(workspaceId, hint.AssetRevisionId, ct);
                if (asset?.Status == AssetRevisionStatus.Published
                    && asset.AssetType is "image" or "visual-hint"
                    && !string.IsNullOrWhiteSpace(asset.Sha256)
                    && (asset.Provenance.RightsExpiresAt is null
                        || asset.Provenance.RightsExpiresAt > DateTimeOffset.UtcNow)) usable++;
                else brokenVisuals++;
                continue;
            }
        }
        if (usable == 0)
            Block(findings, "hints.none-usable", "Hints", "No usable Hint remains",
                "The Round cannot open a fair Guessing Window.", "Repair or add at least one usable Hint.");
        else if (brokenVisuals > 0)
            Warn(findings, "hints.visual-missing", "Hints", "Visual Hint media is unavailable",
                "That visual cannot be shown, but another usable Hint remains.", "Replace the media or explicitly continue without it.");
        else Ready(findings, "hints.ready", "Hints", $"{usable} usable Hint{(usable == 1 ? "" : "s")} ready");
    }

    static IReadOnlyList<ProjectorLyricLine> ValidateLyrics(SongPackageDocument document,
        List<ReadinessFinding> findings)
    {
        if (document.Lyrics is null || string.IsNullOrWhiteSpace(document.Lyrics.Lrc))
        {
            Warn(findings, "lyrics.missing", "Lyrics", "Lyrics are not included",
                "The Projector will show the reveal state instead of synchronized lyrics.",
                "Import or author LRC, or explicitly publish without lyrics.");
            return [];
        }
        if (document.Lyrics.OffsetMs is < -300_000 or > 300_000)
        {
            Block(findings, "lyrics.offset.invalid", "Lyrics", "Lyric offset is outside the supported range",
                "Lines could activate far outside the song.", "Use an offset between -5 and +5 minutes.");
            return [];
        }
        if (!TryParseLrc(document.Lyrics.Lrc, out var parsed, out var error))
        {
            Block(findings, "lyrics.lrc.invalid", "Lyrics", "LRC could not be parsed", error,
                "Correct the timestamped line shown by the editor.");
            return [];
        }
        var lines = parsed.Select(x => new ProjectorLyricLine(
            x.TimestampMs + document.Playback.SongStartOffsetMs + document.Lyrics.OffsetMs, x.Text)).ToArray();
        if (lines.Any(x => x.ActivationMs < 0 || document.Playback.MasterDurationMs is { } duration && x.ActivationMs > duration))
            Block(findings, "lyrics.timeline.outside", "Lyrics", "Lyrics fall outside the master timeline",
                "Some lines can never be displayed during playback.", "Adjust the LRC or lyric offset.");
        else Ready(findings, "lyrics.ready", "Lyrics", $"{lines.Length} timed lyric lines ready for Projector preview");
        return lines;
    }

    public static bool TryParseLrc(string lrc, out IReadOnlyList<(long TimestampMs, string Text)> lines,
        out string error)
    {
        var result = new List<(long, string)>();
        var sourceLines = lrc.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < sourceLines.Length; index++)
        {
            var source = sourceLines[index].Trim();
            if (source.Length == 0 || MetadataLine().IsMatch(source)) continue;
            var matches = Timestamp().Matches(source);
            if (matches.Count == 0) { lines = []; error = $"Line {index + 1} has no [mm:ss.xx] timestamp."; return false; }
            var text = Timestamp().Replace(source, string.Empty).Trim();
            if (text.Length == 0) { lines = []; error = $"Line {index + 1} has no lyric text."; return false; }
            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                result.Add(((long)Math.Round((minutes * 60 + seconds) * 1000), text));
            }
        }
        if (result.Count == 0) { lines = []; error = "No timed lyric lines were found."; return false; }
        lines = result.OrderBy(x => x.Item1).ToArray(); error = string.Empty; return true;
    }

    static bool ValidChannels(IReadOnlyList<int> channels) => channels.All(x => x is >= 1 and <= 64)
        && channels.Distinct().Count() == channels.Count;
    static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    static void Ready(List<ReadinessFinding> target, string code, string section, string title) =>
        target.Add(new(code, ReadinessSeverity.Ready, section, title, string.Empty, string.Empty, false));
    static void Warn(List<ReadinessFinding> target, string code, string section, string title,
        string consequence, string action) => target.Add(new(code, ReadinessSeverity.Warning, section, title,
        consequence, action, true));
    static void Block(List<ReadinessFinding> target, string code, string section, string title,
        string consequence, string action) => target.Add(new(code, ReadinessSeverity.Blocking, section, title,
        consequence, action, false));

    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2}(?:\.\d{1,3})?)\]")]
    private static partial Regex Timestamp();
    [GeneratedRegex(@"^\[[a-zA-Z]+:.*\]$")]
    private static partial Regex MetadataLine();
}
