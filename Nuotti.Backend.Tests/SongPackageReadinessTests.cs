using Nuotti.Backend.Assets;
using Nuotti.Backend.SongPackages;

namespace Nuotti.Backend.Tests;

public sealed class SongPackageReadinessTests
{
    readonly InMemoryPrivateAssetMetadataStore _assets = new();

    [Fact]
    public async Task All_four_playback_modes_use_one_master_timeline()
    {
        var entry = await _assets.CreateEntryAsync("workspace", "Song", "Artist", "member");
        var backing = await PublishedAssetAsync(entry.CatalogEntryId, "backing-track");
        var click = await PublishedAssetAsync(entry.CatalogEntryId, "click-track");
        var evaluator = new SongPackageReadinessEvaluator(_assets);
        var hint = new PackageHint("hint-1", PackageHintType.Text, "Released in the 1980s", null, null);
        var documents = new[]
        {
            Document(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []), hint),
            Document(new(PlaybackMode.ClickOnly, null, click, 0, 10_000, null, 10_000, [], [3]), hint),
            Document(new(PlaybackMode.BackingOnly, backing, null, 2_000, 10_000, 8_000, null, [1, 2], []), hint),
            Document(new(PlaybackMode.BackingWithClick, backing, click, 2_000, 10_000, 8_000, 10_000, [1, 2], [3]), hint)
        };

        foreach (var document in documents)
        {
            var readiness = await evaluator.EvaluateAsync("workspace", document,
                new HashSet<string>(["lyrics.missing"]));
            Assert.True(readiness.CanPublish, string.Join("; ", readiness.Findings.Select(x => x.Code)));
            Assert.DoesNotContain(readiness.Findings, x => x.Severity == ReadinessSeverity.Blocking);
        }
    }

    [Fact]
    public async Task Only_agreed_warnings_can_be_overridden_and_blockers_never_can()
    {
        var evaluator = new SongPackageReadinessEvaluator(_assets);
        var noHints = Document(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []));
        var blocked = await evaluator.EvaluateAsync("workspace", noHints,
            new HashSet<string>(["hints.none", "lyrics.missing"]));
        Assert.False(blocked.CanPublish);
        Assert.False(blocked.Findings.Single(x => x.Code == "hints.none").CanOverride);

        var withHint = Document(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            new("text", PackageHintType.Text, "A decade clue", null, null),
            new("missing-visual", PackageHintType.Image, null, "missing", null));
        var warning = await evaluator.EvaluateAsync("workspace", withHint);
        Assert.False(warning.CanPublish);
        Assert.All(warning.Findings.Where(x => x.Severity == ReadinessSeverity.Warning), x => Assert.True(x.CanOverride));
        var accepted = await evaluator.EvaluateAsync("workspace", withHint,
            new HashSet<string>(["lyrics.missing", "hints.visual-missing"]));
        Assert.True(accepted.CanPublish);
    }

    [Fact]
    public async Task Lrc_import_applies_song_and_lyric_offsets_without_rewriting_source()
    {
        var evaluator = new SongPackageReadinessEvaluator(_assets);
        const string lrc = "[00:01.00]First line\n[00:02.50]Second line";
        var document = new SongPackageDocument(
            new(PlaybackMode.LiveOnly, null, null, 750, null, null, null, [], []),
            [new("hint", PackageHintType.LiveBand, null, null, "Play the opening riff")], new(lrc, 250));

        var readiness = await evaluator.EvaluateAsync("workspace", document);

        Assert.True(readiness.CanPublish);
        Assert.Equal([2_000L, 3_500L], readiness.Preview.Lyrics.Select(x => x.ActivationMs));
        Assert.Equal(lrc, document.Lyrics!.Lrc);
    }

    [Fact]
    public async Task Cross_workspace_asset_revision_is_not_usable()
    {
        var entry = await _assets.CreateEntryAsync("owner-workspace", "Song", "Artist", "member");
        var backing = await PublishedAssetAsync(entry.CatalogEntryId, "backing-track", "owner-workspace");
        var evaluator = new SongPackageReadinessEvaluator(_assets);
        var document = Document(new(PlaybackMode.BackingOnly, backing, null, 0, 8_000, 8_000, null, [1, 2], []),
            new PackageHint("hint", PackageHintType.Text, "Clue", null, null));

        var readiness = await evaluator.EvaluateAsync("other-workspace", document,
            new HashSet<string>(["lyrics.missing"]));

        Assert.Contains(readiness.Findings, x => x.Code == "playback.backing.unusable"
            && x.Severity == ReadinessSeverity.Blocking);
    }

    [Fact]
    public async Task Expired_visual_is_warning_with_fallback_and_blocking_without_one()
    {
        var entry = await _assets.CreateEntryAsync("workspace", "Song", "Artist", "member");
        var provenance = new AssetProvenance("licensed", "license", "NO", ["visual-hint"],
            DateTimeOffset.UtcNow.AddMinutes(-1), "expired-rights");
        var draft = await _assets.CreateDraftAsync("workspace", entry.CatalogEntryId, "visual-hint", "image/png", 4,
            provenance, "member") ?? throw new InvalidOperationException();
        var claim = await _assets.TryBeginFinalizationAsync("workspace", draft.Revision.RevisionId)
            ?? throw new InvalidOperationException();
        var visual = await _assets.PublishAsync("workspace", draft.Revision.RevisionId, "sealed-visual",
            claim.Token, 4, new string('b', 64)) ?? throw new InvalidOperationException();
        var evaluator = new SongPackageReadinessEvaluator(_assets);
        var visualHint = new PackageHint("visual", PackageHintType.Visual, null, visual.RevisionId, null);

        var withoutFallback = await evaluator.EvaluateAsync("workspace",
            Document(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []), visualHint));
        Assert.Contains(withoutFallback.Findings, x => x.Code == "hints.none-usable"
            && x.Severity == ReadinessSeverity.Blocking);

        var withFallback = await evaluator.EvaluateAsync("workspace",
            Document(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []), visualHint,
                new("text", PackageHintType.Text, "Fallback clue", null, null)));
        Assert.Contains(withFallback.Findings, x => x.Code == "hints.visual-missing"
            && x.Severity == ReadinessSeverity.Warning);
    }

    async Task<string> PublishedAssetAsync(string catalogEntryId, string type, string workspace = "workspace")
    {
        var provenance = new AssetProvenance("owned", "original", "NO", [type], null, "rights-case");
        var draft = await _assets.CreateDraftAsync(workspace, catalogEntryId, type, "audio/wav", 4,
            provenance, "member") ?? throw new InvalidOperationException();
        var claim = await _assets.TryBeginFinalizationAsync(workspace, draft.Revision.RevisionId)
            ?? throw new InvalidOperationException();
        var published = await _assets.PublishAsync(workspace, draft.Revision.RevisionId,
            $"sealed-{Guid.NewGuid():N}", claim.Token, 4, new string('a', 64));
        return published!.RevisionId;
    }

    static SongPackageDocument Document(PlaybackConfiguration playback, params PackageHint[] hints) =>
        new(playback, hints, null);
}
