namespace Nuotti.Contracts.V1.Design;

/// <summary>
/// Represents a complete color palette for a theme variant.
/// All colors are in hex format (e.g., "#FF6B35").
/// </summary>
public class ColorPalette
{
    /// <summary>
    /// Primary brand color - vibrant orange (Kahoot-inspired).
    /// </summary>
    public string Primary { get; set; } = string.Empty;
    
    /// <summary>
    /// Secondary brand color - deep blue.
    /// </summary>
    public string Secondary { get; set; } = string.Empty;
    
    /// <summary>
    /// Tertiary accent color - teal.
    /// </summary>
    public string Tertiary { get; set; } = string.Empty;
    
    /// <summary>
    /// Informational color - bright cyan.
    /// </summary>
    public string Info { get; set; } = string.Empty;
    
    /// <summary>
    /// Success color - green.
    /// </summary>
    public string Success { get; set; } = string.Empty;
    
    /// <summary>
    /// Warning color - amber.
    /// </summary>
    public string Warning { get; set; } = string.Empty;
    
    /// <summary>
    /// Error color - pink-red.
    /// </summary>
    public string Error { get; set; } = string.Empty;
    
    /// <summary>
    /// Background color for the application.
    /// </summary>
    public string Background { get; set; } = string.Empty;
    
    /// <summary>
    /// Surface color for cards and elevated elements.
    /// </summary>
    public string Surface { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary text color for body content.
    /// </summary>
    public string TextPrimary { get; set; } = string.Empty;
    
    /// <summary>
    /// Secondary text color for less prominent content.
    /// </summary>
    public string TextSecondary { get; set; } = string.Empty;
    
    /// <summary>
    /// Divider color for separating elements.
    /// </summary>
    public string Divider { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional header background color.
    /// </summary>
    public string? Header { get; set; }
    
    /// <summary>
    /// Optional option/choice background color.
    /// </summary>
    public string? OptionBackground { get; set; }
}
