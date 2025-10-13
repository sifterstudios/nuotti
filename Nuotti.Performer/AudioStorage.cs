using System.Security.Cryptography;
namespace Nuotti.Performer;

public static class AudioStorage
{
    public static string GetAudioRoot()
    {
        // On Windows, use AppData\Roaming\Nuotti\Audio; elsewhere, use ~/Nuotti/Audio
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var root = Path.Combine(appData, "Nuotti", "Audio");
            Directory.CreateDirectory(root);
            return root;
        }
        // Fallback
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeRoot = Path.Combine(home, "Nuotti", "Audio");
        Directory.CreateDirectory(homeRoot);
        return homeRoot;
    }

    public static string GetBlobPath(string sha256Hex, string fileName)
    {
        var root = GetAudioRoot();
        return Path.Combine(root, sha256Hex.ToLowerInvariant(), fileName);
    }

    public static async Task<string> ComputeSha256HexAsync(Stream stream, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        // ensure stream at start if seekable
        if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<(string hash, string storedPath)> ImportFileAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source file not found", sourcePath);
        await using var src = File.OpenRead(sourcePath);
        var hash = await ComputeSha256HexAsync(src, ct);
        var fileName = Path.GetFileName(sourcePath);
        var targetPath = GetBlobPath(hash, fileName);
        var dir = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(dir);
        if (!File.Exists(targetPath))
        {
            // Copy from the beginning
            if (src.CanSeek) src.Seek(0, SeekOrigin.Begin);
            await using var dst = File.Create(targetPath);
            await src.CopyToAsync(dst, ct);
        }
        return (hash, targetPath);
    }

    public static bool IsAvailable(PerformerManifest.SongEntry song)
    {
        // Prefer blob path by hash if available
        if (!string.IsNullOrWhiteSpace(song.Hash))
        {
            var fileName = Path.GetFileName(song.File ?? string.Empty);
            if (!string.IsNullOrEmpty(fileName))
            {
                var blob = GetBlobPath(song.Hash!, fileName);
                if (File.Exists(blob)) return true;
            }
        }
        // Next, check if File is a local path that exists
        var file = song.File;
        if (!string.IsNullOrWhiteSpace(file) && File.Exists(file)) return true;
        return false;
    }
}
