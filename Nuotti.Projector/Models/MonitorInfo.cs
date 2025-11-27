namespace Nuotti.Projector.Models;

public class MonitorInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsPrimary { get; set; }
    
    public string DisplayName => $"{Name} ({Width}x{Height}){(IsPrimary ? " - Primary" : "")}";
}
