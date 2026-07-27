using Nuotti.Contracts.V1.Message;
using Nuotti.Contracts.V1.Model;
namespace Nuotti.Contracts.V1.Event;

/// <summary>
/// Emitted by Backend when a setlist manifest is uploaded and the session's song catalog changes.
/// </summary>
/// <param name="Catalog">The full replacement catalog for the session.</param>
public sealed record CatalogUpdated(IReadOnlyList<SongRef> Catalog) : EventBase;
