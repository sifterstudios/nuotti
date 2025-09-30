namespace Nuotti.Contracts.V1.Model;

/// <summary>
/// Lightweight reference to a song used across messages and state.
/// </summary>
/// <param name="Id">Stable song identifier.</param>
/// <param name="Title">Display-title of the song.</param>
/// <param name="Artist">Primary artist/performer</param>
public sealed record SongRef(SongId Id, string Title, string Artist);