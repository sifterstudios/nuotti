using System.Security.Cryptography;
using System.Text.Json;
using Nuotti.Contracts.V1.Message;

namespace Nuotti.AudioEngine;

public sealed record VenueCacheFinding(string Code, bool Blocking, bool CanOverride, string Detail);
public sealed record VenueCachePreflight(bool Ready, IReadOnlyList<VenueCacheFinding> Findings,
    IReadOnlyDictionary<string, string> LocalPaths);

public sealed class VenueAssetCache(HttpClient downloads, string rootDirectory)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<VenueCachePreflight> PrepareAsync(CloudSessionSnapshot snapshot,
        Func<string, CancellationToken, Task<CloudAssetGrant>> grantFor,
        IReadOnlySet<string>? acceptedWarnings = null, CancellationToken ct = default)
    {
        acceptedWarnings ??= new HashSet<string>();
        var findings = new List<VenueCacheFinding>();
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        Directory.CreateDirectory(rootDirectory);
        var manifestPath = Path.Combine(rootDirectory, "snapshot.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CachedSnapshot>(await File.ReadAllTextAsync(manifestPath, ct), Json);
                if (cached is null) throw new JsonException("Manifest is empty.");
                if (cached.WorkspaceId != snapshot.WorkspaceId || cached.SessionCode != snapshot.SessionCode
                    || cached.SnapshotId != snapshot.SnapshotId)
                    findings.Add(new("cache.stale-snapshot", true, false,
                        $"Venue cache belongs to {cached.WorkspaceId}/{cached.SessionCode}/{cached.SnapshotId}, not {snapshot.WorkspaceId}/{snapshot.SessionCode}/{snapshot.SnapshotId}."));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                findings.Add(new("cache.manifest-invalid", true, false,
                    $"Venue cache manifest is damaged or unreadable: {ex.Message}"));
            }
        }

        foreach (var asset in snapshot.Assets)
        {
            var extension = asset.AssetType is "backing-track" or "click-track" ? ".audio" : ".media";
            var path = Path.Combine(rootDirectory, Safe(asset.RevisionId) + extension);
            if (!await ValidAsync(path, asset, ct))
            {
                try
                {
                    var grant = await grantFor(asset.RevisionId, ct);
                    var temp = path + ".partial";
                    using var response = await downloads.GetAsync(grant.DownloadUri, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength is { } length && length != asset.Size)
                        throw new InvalidDataException($"Content-Length {length} does not match expected {asset.Size} bytes.");
                    await using (var destination = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                        await CopyBoundedAsync(await response.Content.ReadAsStreamAsync(ct), destination, asset.Size, ct);
                    if (!await ValidAsync(temp, asset, ct))
                    {
                        File.Delete(temp);
                        findings.Add(new($"asset.{asset.RevisionId}.hash-mismatch", asset.Required, !asset.Required,
                            $"Downloaded {asset.AssetType} bytes do not match the immutable snapshot hash and size."));
                        continue;
                    }
                    File.Move(temp, path, true);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException)
                {
                    try { File.Delete(path + ".partial"); } catch { }
                    findings.Add(new($"asset.{asset.RevisionId}.download-failed", asset.Required, !asset.Required,
                        $"{asset.AssetType} could not be cached: {ex.Message}"));
                    continue;
                }
            }
            paths[asset.RevisionId] = path;
        }
        var ready = findings.All(x => !x.Blocking && (!x.CanOverride || acceptedWarnings.Contains(x.Code)))
            && snapshot.Assets.Where(x => x.Required)
            .All(x => paths.ContainsKey(x.RevisionId));
        if (ready)
        {
            var tempManifest = manifestPath + ".partial";
            await File.WriteAllTextAsync(tempManifest,
                JsonSerializer.Serialize(new CachedSnapshot(snapshot.SnapshotId, snapshot.WorkspaceId, snapshot.SessionCode), Json), ct);
            File.Move(tempManifest, manifestPath, true);
        }
        return new(ready, findings, paths);
    }

    static async Task<bool> ValidAsync(string path, CloudSnapshotAsset asset, CancellationToken ct)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != asset.Size) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        return digest.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    static string Safe(string value) => string.Concat(value.Select(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' ? x : '_'));
    static async Task CopyBoundedAsync(Stream source, Stream destination, long expectedSize, CancellationToken ct)
    {
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct); if (read == 0) break;
            total += read; if (total > expectedSize) throw new InvalidDataException("Download exceeded immutable asset size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (total != expectedSize) throw new InvalidDataException($"Download ended at {total} of {expectedSize} bytes.");
    }
    sealed record CachedSnapshot(string SnapshotId, string WorkspaceId, string SessionCode);

    public static bool TryResolveCapturedSource(PlayTrack command, IReadOnlyDictionary<string, string> localPaths,
        out string source)
    {
        var revisionId = !string.IsNullOrWhiteSpace(command.AssetRevisionId) ? command.AssetRevisionId : command.FileUrl;
        return localPaths.TryGetValue(revisionId, out source!);
    }
}
