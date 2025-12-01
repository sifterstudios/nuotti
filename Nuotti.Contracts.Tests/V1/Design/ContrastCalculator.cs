using Nuotti.Contracts.V1.Design;

namespace Nuotti.Contracts.Tests.V1.Design;

/// <summary>
/// Utility class for calculating contrast ratios between colors for WCAG compliance testing.
/// </summary>
public static class ContrastCalculator
{
    /// <summary>
    /// Calculates the contrast ratio between two hex colors.
    /// Returns a value from 1:1 (no contrast) to 21:1 (maximum contrast).
    /// WCAG AA requires 4.5:1 for normal text and 3:1 for large text.
    /// </summary>
    public static double CalculateContrastRatio(string foregroundHex, string backgroundHex)
    {
        var fgRgb = HexToRgb(foregroundHex);
        var bgRgb = HexToRgb(backgroundHex);
        
        var fgLuminance = CalculateRelativeLuminance(fgRgb);
        var bgLuminance = CalculateRelativeLuminance(bgRgb);
        
        // Contrast ratio = (L1 + 0.05) / (L2 + 0.05)
        // where L1 is the lighter color and L2 is the darker color
        var lighter = Math.Max(fgLuminance, bgLuminance);
        var darker = Math.Min(fgLuminance, bgLuminance);
        
        return (lighter + 0.05) / (darker + 0.05);
    }
    
    /// <summary>
    /// Calculates the relative luminance of an RGB color (0.0 to 1.0).
    /// </summary>
    private static double CalculateRelativeLuminance((int r, int g, int b) rgb)
    {
        // Convert to linear values
        double RsRGB = rgb.r / 255.0;
        double GsRGB = rgb.g / 255.0;
        double BsRGB = rgb.b / 255.0;
        
        double R = RsRGB <= 0.03928 ? RsRGB / 12.92 : Math.Pow((RsRGB + 0.055) / 1.055, 2.4);
        double G = GsRGB <= 0.03928 ? GsRGB / 12.92 : Math.Pow((GsRGB + 0.055) / 1.055, 2.4);
        double B = BsRGB <= 0.03928 ? BsRGB / 12.92 : Math.Pow((BsRGB + 0.055) / 1.055, 2.4);
        
        // Relative luminance formula
        return 0.2126 * R + 0.7152 * G + 0.0722 * B;
    }
    
    /// <summary>
    /// Converts a hex color string (e.g., "#FF6B35") to RGB tuple.
    /// </summary>
    private static (int r, int g, int b) HexToRgb(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex color cannot be null or empty", nameof(hex));
        
        // Remove # if present
        hex = hex.TrimStart('#');
        
        if (hex.Length != 6)
            throw new ArgumentException($"Invalid hex color format: #{hex}", nameof(hex));
        
        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
        
        return (r, g, b);
    }
    
    /// <summary>
    /// Checks if the contrast ratio meets WCAG AA standards for normal text (4.5:1).
    /// </summary>
    public static bool MeetsWCAGAA(double contrastRatio)
    {
        return contrastRatio >= 4.5;
    }
    
    /// <summary>
    /// Checks if the contrast ratio meets WCAG AA standards for large text (3:1).
    /// </summary>
    public static bool MeetsWCAGAALarge(double contrastRatio)
    {
        return contrastRatio >= 3.0;
    }
    
    /// <summary>
    /// Checks if the contrast ratio meets WCAG AAA standards for normal text (7:1).
    /// </summary>
    public static bool MeetsWCAGAAA(double contrastRatio)
    {
        return contrastRatio >= 7.0;
    }
}
