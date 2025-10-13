using System.Text.Json;
namespace Nuotti.Performer;

public sealed class ManifestService : IManifestService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Nuotti");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "performer-manifest.json");
    }

    public async Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default)
    {
        path ??= GetDefaultPath();
        if (!File.Exists(path))
        {
            return new PerformerManifest();
        }
        await using var fs = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<PerformerManifest>(fs, _jsonOptions, ct);
        return manifest ?? new PerformerManifest();
    }

    public async Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default)
    {
        path ??= GetDefaultPath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        await using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, manifest, _jsonOptions, ct);
    }
}
