using System.Collections.Concurrent;
using Nuotti.Contracts.V1.Model;

namespace Nuotti.Backend.Sessions;

public sealed class InMemoryGameStateStore : IGameStateStore
{
    private readonly ConcurrentDictionary<string, GameStateSnapshot> _states = new();

    public bool TryGet(string session, out GameStateSnapshot snapshot)
        => _states.TryGetValue(session, out snapshot!);

    public GameStateSnapshot GetOrCreate(string session, Func<string, GameStateSnapshot> factory)
        => _states.GetOrAdd(session, factory);

    public void Set(string session, GameStateSnapshot snapshot)
        => _states[session] = snapshot;
}