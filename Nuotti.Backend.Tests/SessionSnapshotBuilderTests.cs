using Nuotti.Backend.Assets;
using Nuotti.Backend.SessionSnapshots;
using Nuotti.Backend.SongPackages;

namespace Nuotti.Backend.Tests;

public sealed class SessionSnapshotBuilderTests
{
    readonly InMemoryPrivateAssetMetadataStore _assets = new();
    readonly InMemorySongPackageStore _packages = new();
    readonly InMemoryLyricTrackRevisionStore _lyrics = new();
    readonly MvpScoringPolicyCatalog _policies = new();

    [Fact]
    public async Task Captures_exact_revision_hint_order_lyrics_scoring_and_verified_assets()
    {
        var entry = await _assets.CreateEntryAsync("ws", "Song", "Artist", "member");
        var backing = await PublishedAsync(entry.CatalogEntryId, "backing-track", [1, 2, 3, 4]);
        var document = new SongPackageDocument(new(PlaybackMode.BackingOnly, backing.RevisionId, null, 1000,
            5000, 4000, null, [1, 2], []),
            [new("h1", PackageHintType.Text, "First", null, null), new("h2", PackageHintType.LiveBand, null, null, "Riff")],
            new("[00:01.00]Line", 250));
        await _packages.SaveDraftAsync("ws", entry.CatalogEntryId, document, "member");
        var revision = await _packages.PublishAsync("ws", entry.CatalogEntryId, document, "ready", [], "member");
        await _lyrics.PublishAsync("ws", entry.CatalogEntryId, document.Lyrics!.Lrc, document.Lyrics.OffsetMs, "member");
        var policy = new ScoringPolicyReference("standard", 1);

        var built = await new SessionSnapshotBuilder(_packages, _assets, _lyrics, _policies).BuildAsync("ws",
            [new(revision.RevisionId)], policy, new HashSet<string>());

        Assert.True(built.Preflight.CanCreate);
        var song = Assert.Single(built.Songs);
        Assert.Equal(revision.RevisionId, song.PackageRevisionId);
        Assert.Equal(["h1", "h2"], song.Hints.Select(x => x.HintId));
        Assert.Equal(document.Lyrics!.Lrc, song.LyricTrack!.Lrc);
        Assert.StartsWith("lyric_", song.LyricTrack.TrackRevisionId);
        Assert.Equal(64, song.LyricTrack.Sha256.Length);
        Assert.Equal(backing.Sha256, Assert.Single(built.Preflight.Assets).Sha256);
    }

    [Fact]
    public async Task Missing_lyrics_is_safe_override_but_missing_revision_is_never_overridable()
    {
        var entry = await _assets.CreateEntryAsync("ws", "Live", "Band", "member");
        var document = new SongPackageDocument(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("h", PackageHintType.Text, "Clue", null, null)], null);
        await _packages.SaveDraftAsync("ws", entry.CatalogEntryId, document, "member");
        var revision = await _packages.PublishAsync("ws", entry.CatalogEntryId, document, "ready", [], "member");
        var builder = new SessionSnapshotBuilder(_packages, _assets, _lyrics, _policies);
        var policy = new ScoringPolicyReference("standard", 1);
        var warning = await builder.BuildAsync("ws", [new(revision.RevisionId)], policy, new HashSet<string>());
        Assert.False(warning.Preflight.CanCreate);
        var code = Assert.Single(warning.Preflight.Findings.Where(x => x.Severity == ReadinessSeverity.Warning)).Code;
        Assert.True((await builder.BuildAsync("ws", [new(revision.RevisionId)], policy, new HashSet<string>([code]))).Preflight.CanCreate);
        var blocked = await builder.BuildAsync("ws", [new("missing")], policy, new HashSet<string>(["song.1.revision-missing"]));
        Assert.False(blocked.Preflight.CanCreate);
        Assert.False(blocked.Preflight.Findings.Single(x => x.Code == "song.1.revision-missing").CanOverride);
    }

    [Fact]
    public async Task Explicit_independent_lyric_revision_is_captured_even_after_later_edits()
    {
        var entry = await _assets.CreateEntryAsync("ws", "Song", "Artist", "member");
        var document = new SongPackageDocument(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("h", PackageHintType.Text, "Clue", null, null)], null);
        await _packages.SaveDraftAsync("ws", entry.CatalogEntryId, document, "member");
        var package = await _packages.PublishAsync("ws", entry.CatalogEntryId, document, "ready", [], "member");
        var first = await _lyrics.PublishAsync("ws", entry.CatalogEntryId, "[00:01.00]First", 0, "member");
        await _lyrics.PublishAsync("ws", entry.CatalogEntryId, "[00:01.00]Later edit", 250, "member");

        var built = await new SessionSnapshotBuilder(_packages, _assets, _lyrics, _policies).BuildAsync("ws",
            [new(package.RevisionId, first.RevisionId)], new("standard", 1), new HashSet<string>());

        Assert.True(built.Preflight.CanCreate);
        Assert.Equal(first.RevisionId, Assert.Single(built.Songs).LyricTrack!.TrackRevisionId);
        Assert.Equal("[00:01.00]First", built.Songs[0].LyricTrack!.Lrc);
    }

    [Fact]
    public async Task Unknown_scoring_policy_version_is_blocking()
    {
        var built = await new SessionSnapshotBuilder(_packages, _assets, _lyrics, _policies).BuildAsync("ws",
            [new("missing")], new("standard", 999), new HashSet<string>());
        Assert.False(built.Preflight.CanCreate);
        Assert.Contains(built.Preflight.Findings, x => x.Code == "scoring.invalid" && !x.CanOverride);
    }

    [Fact]
    public async Task Publishing_historical_lyric_content_again_makes_a_new_current_revision()
    {
        var entry = await _assets.CreateEntryAsync("ws", "Song", "Artist", "member");
        var first = await _lyrics.PublishAsync("ws", entry.CatalogEntryId, "[00:01.00]A", 0, "member");
        var second = await _lyrics.PublishAsync("ws", entry.CatalogEntryId, "[00:01.00]B", 0, "member");
        var reverted = await _lyrics.PublishAsync("ws", entry.CatalogEntryId, "[00:01.00]A", 0, "member");
        Assert.Equal(first.Sha256, reverted.Sha256);
        Assert.Equal(second.Version + 1, reverted.Version);
        Assert.Equal(reverted.RevisionId, (await _lyrics.GetCurrentAsync("ws", entry.CatalogEntryId))!.RevisionId);
    }

    [Fact]
    public async Task Visual_only_hint_is_required_in_the_venue_manifest()
    {
        var entry = await _assets.CreateEntryAsync("ws", "Visual Song", "Artist", "member");
        var visual = await PublishedAsync(entry.CatalogEntryId, "visual-hint", [1, 2, 3]);
        var document = new SongPackageDocument(new(PlaybackMode.LiveOnly, null, null, 0, null, null, null, [], []),
            [new("visual", PackageHintType.Visual, null, visual.RevisionId, null)], null);
        await _packages.SaveDraftAsync("ws", entry.CatalogEntryId, document, "member");
        var package = await _packages.PublishAsync("ws", entry.CatalogEntryId, document, "ready", [], "member");
        var warning = "song.1.lyrics-missing";
        var built = await new SessionSnapshotBuilder(_packages, _assets, _lyrics, _policies).BuildAsync("ws",
            [new(package.RevisionId)], new("standard", 1), new HashSet<string>([warning]));
        Assert.True(built.Preflight.CanCreate);
        Assert.True(Assert.Single(built.Preflight.Assets).Required);
        Assert.DoesNotContain(built.Preflight.Findings, finding =>
            finding.Title.Contains("required audio", StringComparison.OrdinalIgnoreCase));
    }

    async Task<PrivateAssetRevision> PublishedAsync(string entryId, string type, byte[] bytes)
    {
        var provenance = new AssetProvenance("owned", "original", "NO", [type], null, "evidence");
        var draft = await _assets.CreateDraftAsync("ws", entryId, type, "audio/wav", bytes.Length, provenance, "member")
            ?? throw new InvalidOperationException();
        var claim = await _assets.TryBeginFinalizationAsync("ws", draft.Revision.RevisionId) ?? throw new InvalidOperationException();
        return await _assets.PublishAsync("ws", draft.Revision.RevisionId, "sealed", claim.Token, bytes.Length,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()) ?? throw new InvalidOperationException();
    }
}
