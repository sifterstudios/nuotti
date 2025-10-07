using Nuotti.Contracts.V1.Model;
namespace Nuotti.Backend.Sessions;

public interface IGameStateStore
{
    bool TryGet(string session, out GameStateSnapshot snapshot);
    GameStateSnapshot GetOrCreate(string session, Func<string, GameStateSnapshot> factory);
    void Set(string session, GameStateSnapshot snapshot);
    void Remove(string session);
}