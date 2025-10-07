using Microsoft.AspNetCore.SignalR;
using Nuotti.Backend.Models;
using System.Collections.Concurrent;
namespace Nuotti.Backend;

public class GameHub : Hub
{
    static readonly ConcurrentDictionary<string, Player> players = new ConcurrentDictionary<string, Player>();
    static readonly GameState gameState = new GameState();
    static readonly List<Song> songs = [];

    public async override Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("ReceiveGameState", gameState);
    }

    public async override Task OnDisconnectedAsync(System.Exception? exception)
    {
        if (players.TryRemove(Context.ConnectionId, out var player))
        {
            gameState.Players.Remove(player.ConnectionId);
            await Clients.All.SendAsync("PlayerDisconnected", player.Nickname);
            await Clients.All.SendAsync("ReceiveGameState", gameState);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGame(string nickname)
    {
        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            Nickname = nickname,
            Score = 0,
            IsConnected = true,
            JoinedAt = DateTime.UtcNow
        };

        if (players.TryAdd(Context.ConnectionId, player))
        {
            gameState.Players[Context.ConnectionId] = player;
            await Clients.All.SendAsync("PlayerJoined", player.Nickname);
            await Clients.All.SendAsync("ReceiveGameState", gameState);
        }
    }

    public async Task StartGame()
    {
        gameState.CurrentPhase = GamePhase.Hinting;
        gameState.GameStartTime = DateTime.UtcNow;
        await Clients.All.SendAsync("GameStarted");
        await Clients.All.SendAsync("ReceiveGameState", gameState);
    }

    public async Task SelectSong(string songId)
    {
        var song = songs.FirstOrDefault(s => s.Id == songId);
        if (song != null)
        {
            gameState.CurrentSong = song;
            gameState.CurrentPhase = GamePhase.Guessing;
            await Clients.All.SendAsync("SongSelected", song);
            await Clients.All.SendAsync("ReceiveGameState", gameState);
        }
    }

    public async Task SubmitGuess(string guess)
    {
        if (gameState.CurrentSong == null) return;

        var isCorrect = gameState.CurrentSong.Title.Equals(guess, StringComparison.OrdinalIgnoreCase) ||
                        gameState.CurrentSong.Artist.Equals(guess, StringComparison.OrdinalIgnoreCase);

        if (isCorrect && players.TryGetValue(Context.ConnectionId, out var player))
        {
            player.Score += 100; // Award points for correct guess
            await Clients.All.SendAsync("GuessResult", player.Nickname, true);
            await Clients.All.SendAsync("ReceiveGameState", gameState);
        }
        else
        {
            await Clients.Caller.SendAsync("GuessResult", "Incorrect guess", false);
        }
    }

    public async Task StartPlayback()
    {
        if (gameState.CurrentSong == null) return;

        gameState.CurrentPhase = GamePhase.Playing;
        gameState.IsPlaying = true;
        await Clients.All.SendAsync("PlaybackStarted");
        await Clients.All.SendAsync("ReceiveGameState", gameState);
    }

    public async Task UpdatePlaybackPosition(TimeSpan position)
    {
        gameState.CurrentPlaybackPosition = position;
        await Clients.All.SendAsync("PlaybackPositionUpdated", position);
    }

    public async Task EndGame()
    {
        gameState.CurrentPhase = GamePhase.Results;
        gameState.IsPlaying = false;
        await Clients.All.SendAsync("GameEnded");
        await Clients.All.SendAsync("ReceiveGameState", gameState);
    }
}