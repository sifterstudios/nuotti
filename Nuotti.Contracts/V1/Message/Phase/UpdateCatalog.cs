using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Message.Phase;

/// <summary>
/// Replaces the session's song catalog from an uploaded setlist manifest.
/// Allowed in any phase: a Performer may re-upload a setlist at any point.
/// </summary>
/// <param name="Manifest">The uploaded manifest. Validated when the command is applied.</param>
public sealed record UpdateCatalog(SetlistManifest Manifest) : CommandBase
{
    public SetlistManifest Manifest { get; } = Manifest;
}
