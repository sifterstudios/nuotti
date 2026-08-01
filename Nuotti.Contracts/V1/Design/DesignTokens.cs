namespace Nuotti.Contracts.V1.Design;

/// <summary>
/// Centralized design token system for Nuotti applications.
/// Provides consistent colors, spacing, and typography across all apps.
/// </summary>
public static class DesignTokens
{
    /// <summary>
    /// Gets the color palette for the specified theme variant.
    /// </summary>
    public static ColorPalette GetPalette(ThemeVariant variant)
    {
        return variant switch
        {
            ThemeVariant.Light => LightPalette,
            ThemeVariant.Dark => DarkPalette,
            ThemeVariant.HighContrast => HighContrastPalette,
            _ => LightPalette
        };
    }
    
    /// <summary>
    /// Light theme color palette (Kahoot/Bandle-inspired).
    /// </summary>
    public static ColorPalette LightPalette => new()
    {
        // Deepened from #FF6B35 so white button text meets WCAG AA: 2.84:1 -> 4.53:1.
        // MudBlazor renders white text on Color.Primary, so the old orange failed in practice.
        Primary = "#DB3B00",
        Secondary = "#004E89",
        Tertiary = "#1B9AAA",
        Info = "#06BEE1",
        // Deepened from #46B283 to clear WCAG AA large-text on the light background: 2.53:1 -> 3.02:1.
        Success = "#40A277",
        Warning = "#F77F00",
        Error = "#EF476F",
        Background = "#FAFAFA",
        Surface = "#FFFFFF",
        TextPrimary = "#1A1A1A",
        TextSecondary = "#666666",
        Divider = "#E0E0E0",
        Header = "#FFFFFF",
        OptionBackground = "#F5F5F5"
    };
    
    /// <summary>
    /// Dark theme color palette (Bandle-inspired).
    /// </summary>
    public static ColorPalette DarkPalette => new()
    {
        // Deepened from #FF8C61 so white button text meets WCAG AA: 2.29:1 -> 4.51:1. Note this
        // trades accent legibility on the dark background down to 4.22:1 - acceptable for large
        // text and UI shapes, below AA for small text.
        Primary = "#DB3C00",
        Secondary = "#2E7DAF",
        Tertiary = "#48C9B0",
        Info = "#3DD9FF",
        Success = "#5EC99D",
        Warning = "#FFA040",
        Error = "#FF6B93",
        Background = "#0A0E27",
        Surface = "#151B3B",
        TextPrimary = "#E8E8E8",
        TextSecondary = "#B0B0B0",
        Divider = "#2A2F4F",
        Header = "#151B3B",
        OptionBackground = "#1F2544"
    };
    
    /// <summary>
    /// High-contrast theme color palette meeting WCAG AA standards.
    /// Ensures minimum 4.5:1 contrast ratio for normal text and 3:1 for large text.
    /// </summary>
    public static ColorPalette HighContrastPalette => new()
    {
        Primary = "#A62C00",      // Deepened from #FF6B35: white text 2.84:1 -> 7.04:1 (AAA)
        Secondary = "#0066CC",     // Deep blue with high contrast
        Tertiary = "#0088CC",      // High contrast teal
        Info = "#0066CC",          // High contrast cyan-blue
        Success = "#008800",       // High contrast green
        Warning = "#CC6600",       // High contrast amber
        Error = "#CC0000",         // High contrast red
        Background = "#FFFFFF",    // Pure white for maximum contrast
        Surface = "#FFFFFF",       // Pure white surface
        TextPrimary = "#000000",   // Pure black for maximum contrast (21:1 ratio)
        TextSecondary = "#333333", // Dark gray with high contrast on white (12.6:1 ratio)
        Divider = "#666666",       // Medium gray divider
        Header = "#FFFFFF",        // White header
        OptionBackground = "#F0F0F0" // Light gray for subtle distinction
    };
    
    /// <summary>
    /// Projector Variant B — dark stage with accessible cyan (#00FFF5). Selected for venue display.
    /// Button labels use <see cref="ColorPalette.OnPrimary"/> (dark ink); white-on-cyan fails WCAG.
    /// </summary>
    public static ColorPalette ProjectorVariantBPalette => new()
    {
        Primary = "#00FFF5",
        Secondary = "#17363A",
        Tertiary = "#48C9B0",
        Info = "#00FFF5",
        Success = "#5EC99D",
        Warning = "#FFA040",
        Error = "#FF6B93",
        Background = "#05090B",
        Surface = "#091115",
        TextPrimary = "#ECFFFF",
        TextSecondary = "#91A5AA",
        Divider = "#17363A",
        Header = "#091115",
        OptionBackground = "#0C181D",
        OnPrimary = "#001413"
    };

    /// <summary>
    /// Border radius for rounded corners (12px).
    /// </summary>
    public const int DefaultBorderRadius = 12;
    
    /// <summary>
    /// Gets the border radius as a pixel string.
    /// </summary>
    public static string BorderRadius => $"{DefaultBorderRadius}px";
}
