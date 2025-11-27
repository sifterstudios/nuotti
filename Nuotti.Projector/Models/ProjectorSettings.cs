using System.Text.Json.Serialization;

namespace Nuotti.Projector.Models;

public class ProjectorSettings
{
    [JsonPropertyName("selectedMonitorId")]
    public string? SelectedMonitorId { get; set; }
    
    [JsonPropertyName("isFullscreen")]
    public bool IsFullscreen { get; set; }
    
    [JsonPropertyName("safeAreaMargin")]
    public double SafeAreaMargin { get; set; } = 0.05; // 5% default
    
    [JsonPropertyName("showSafeAreaFrame")]
    public bool ShowSafeAreaFrame { get; set; }
    
    [JsonPropertyName("hideTalliesUntilReveal")]
    public bool HideTalliesUntilReveal { get; set; }
    
    [JsonPropertyName("themeVariant")]
    public string ThemeVariant { get; set; } = "Default";
    
    [JsonPropertyName("tallyMode")]
    public string TallyMode { get; set; } = "Animated";
    
    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en";
    
    [JsonPropertyName("alwaysOnTop")]
    public bool AlwaysOnTop { get; set; }
    
    [JsonPropertyName("cursorHidden")]
    public bool CursorHidden { get; set; }
}
