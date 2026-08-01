using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.AudioEngine.Tests;

public sealed class VenueAssetCacheTests
{
    [Fact]
    public async Task Downloads_required_asset_and_verifies_hash_before_ready()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var asset = Asset(bytes, required: true);
        var root = Temp();
        try
        {
            using var http = new HttpClient(new BytesHandler(bytes));
            var cache = new VenueAssetCache(http, root);
            var result = await cache.PrepareAsync(Snapshot("snap-1", asset),
                (_, _) => Task.FromResult(new CloudAssetGrant(new Uri("https://objects.test/file"), DateTimeOffset.MaxValue)));
            Assert.True(result.Ready);
            Assert.True(File.Exists(result.LocalPaths[asset.RevisionId]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Corrupt_required_download_blocks_preflight_and_is_not_promoted()
    {
        var asset = Asset([1, 2, 3], required: true);
        var root = Temp();
        try
        {
            using var http = new HttpClient(new BytesHandler([9, 9, 9]));
            var result = await new VenueAssetCache(http, root).PrepareAsync(Snapshot("snap-1", asset),
                (_, _) => Task.FromResult(new CloudAssetGrant(new Uri("https://objects.test/file"), DateTimeOffset.MaxValue)));
            Assert.False(result.Ready);
            Assert.Contains(result.Findings, x => x.Code.Contains("hash-mismatch") && x.Blocking);
            Assert.Empty(result.LocalPaths);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Different_snapshot_for_same_session_is_rejected_as_stale()
    {
        var root = Temp();
        try
        {
            var empty = new CloudSessionSnapshot("snap-1", "ws", "SHOW", 1, []);
            var cache = new VenueAssetCache(new HttpClient(new BytesHandler([])), root);
            Assert.True((await cache.PrepareAsync(empty, (_, _) => throw new InvalidOperationException())).Ready);
            var stale = await cache.PrepareAsync(empty with { SnapshotId = "snap-2" },
                (_, _) => throw new InvalidOperationException());
            Assert.False(stale.Ready);
            Assert.Contains(stale.Findings, x => x.Code == "cache.stale-snapshot" && x.Blocking);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Manifest_from_another_session_is_rejected()
    {
        var root = Temp();
        try
        {
            var cache = new VenueAssetCache(new HttpClient(new BytesHandler([])), root);
            Assert.True((await cache.PrepareAsync(new("snap-1", "ws", "OTHER", 1, []),
                (_, _) => throw new InvalidOperationException())).Ready);
            var result = await cache.PrepareAsync(Snapshot("snap-1"), (_, _) => throw new InvalidOperationException());
            Assert.False(result.Ready);
            Assert.Contains(result.Findings, x => x.Code == "cache.stale-snapshot" && x.Blocking);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Optional_media_failure_requires_the_named_safe_override()
    {
        var asset = Asset([1, 2, 3], required: false);
        var root = Temp();
        try
        {
            using var http = new HttpClient(new BytesHandler([9, 9, 9]));
            var cache = new VenueAssetCache(http, root);
            var blocked = await cache.PrepareAsync(Snapshot("snap-1", asset),
                (_, _) => Task.FromResult(new CloudAssetGrant(new Uri("https://objects.test/file"), DateTimeOffset.MaxValue)));
            var warning = Assert.Single(blocked.Findings);
            Assert.False(blocked.Ready);
            Assert.False(warning.Blocking);
            Assert.True(warning.CanOverride);
            var accepted = await cache.PrepareAsync(Snapshot("snap-1", asset),
                (_, _) => Task.FromResult(new CloudAssetGrant(new Uri("https://objects.test/file"), DateTimeOffset.MaxValue)),
                new HashSet<string> { warning.Code });
            Assert.True(accepted.Ready);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Playback_resolves_only_a_revision_present_in_the_verified_cache()
    {
        var paths = new Dictionary<string, string> { ["rev-1"] = @"C:\cache\rev-1.audio" };
        Assert.True(VenueAssetCache.TryResolveCapturedSource(Play("ignored") with { AssetRevisionId = "rev-1" },
            paths, out var source));
        Assert.Equal(paths["rev-1"], source);
        Assert.False(VenueAssetCache.TryResolveCapturedSource(Play("https://outside.test/file"), paths, out _));
    }

    [Fact]
    public async Task Corrupt_manifest_is_a_named_blocker_not_an_exception()
    {
        var root = Temp();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "snapshot.json"), "{torn");
            var result = await new VenueAssetCache(new HttpClient(new BytesHandler([])), root)
                .PrepareAsync(Snapshot("snap-1"), (_, _) => throw new InvalidOperationException());
            Assert.False(result.Ready);
            Assert.Contains(result.Findings, x => x.Code == "cache.manifest-invalid" && x.Blocking);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Oversized_body_is_stopped_and_never_promoted()
    {
        var asset = Asset([1, 2, 3], required: true); var root = Temp();
        try
        {
            var result = await new VenueAssetCache(new HttpClient(new BytesHandler(new byte[1000], true)), root)
                .PrepareAsync(Snapshot("snap-1", asset),
                    (_, _) => Task.FromResult(new CloudAssetGrant(new Uri("https://objects.test/file"), DateTimeOffset.MaxValue)));
            Assert.False(result.Ready); Assert.Empty(result.LocalPaths);
            Assert.False(File.Exists(Path.Combine(root, "rev-1.audio.partial")));
        }
        finally { Directory.Delete(root, true); }
    }

    static CloudSnapshotAsset Asset(byte[] bytes, bool required) => new("rev-1", "backing-track",
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length, required);
    static PlayTrack Play(string file) => new(file)
    { SessionCode = "SHOW", IssuedById = "performer", IssuedByRole = Nuotti.Contracts.V1.Enum.Role.Performer };
    static CloudSessionSnapshot Snapshot(string id, params CloudSnapshotAsset[] assets) => new(id, "ws", "SHOW", 1, assets);
    static string Temp() { var path = Path.Combine(Path.GetTempPath(), $"nuotti-cache-{Guid.NewGuid():N}"); Directory.CreateDirectory(path); return path; }
    sealed class BytesHandler(byte[] bytes, bool omitLength = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(Response());
        HttpResponseMessage Response()
        {
            var content = new ByteArrayContent(bytes);
            if (omitLength) content.Headers.ContentLength = null;
            return new(HttpStatusCode.OK) { Content = content };
        }
    }
}
