namespace Nuotti.Backend;
    using Microsoft.AspNetCore.SignalR;
    using System.Threading.Tasks;

    public class GameHub : Hub
    {
        public async Task SendSongUpdate(string song)
        {
            await Clients.All.SendAsync("ReceiveSongUpdate", song);
        }

        public async Task SendScoreUpdate(string player, int score)
        {
            await Clients.All.SendAsync("ReceiveScoreUpdate", player, score);
        }
    }
