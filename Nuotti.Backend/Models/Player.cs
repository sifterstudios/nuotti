namespace Nuotti.Backend.Models;

public record Player
{
    public string ConnectionId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public int Score { get; set; }
    public bool IsConnected { get; set; }
    public DateTime JoinedAt { get; set; }
} 