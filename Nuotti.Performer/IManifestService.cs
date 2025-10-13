namespace Nuotti.Performer;

public interface IManifestService
{
    string GetDefaultPath();
    Task<PerformerManifest> LoadAsync(string? path = null, CancellationToken ct = default);
    Task SaveAsync(PerformerManifest manifest, string? path = null, CancellationToken ct = default);
}
