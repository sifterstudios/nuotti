namespace Nuotti.Contracts.V1.Design;

/// <summary>
/// Centralized design token system for Nuotti applications.
/// Neon cyan single-accent on stage-dark surfaces; sharp (0) corners throughout.
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
    /// Light theme — cool stage wash with deepened cyan primary (usable as text on white).
    /// Bright neon lives in <see cref="DarkPalette"/> / <see cref="ProjectorVariantBPalette"/>.
    /// </summary>
    public static ColorPalette LightPalette => new()
    {
        Primary = "#007A74",
        Secondary = "#0D3A40",
        Tertiary = "#1B9AAA",
        // Match Primary so Info-colored text/buttons meet WCAG AA on white (~5.2:1).
        Info = "#007A74",
        Success = "#1F7A5C",
        Warning = "#B35C00",
        Error = "#C41E4A",
        Background = "#F0F7F8",
        Surface = "#FFFFFF",
        TextPrimary = "#0A1518",
        TextSecondary = "#4A6068",
        Divider = "#C5D4D8",
        Header = "#FFFFFF",
        OptionBackground = "#E5F0F2",
        OnPrimary = "#FFFFFF"
    };

    /// <summary>
    /// Dark theme — neon cyan single accent on near-black stage (matches song-package / venue).
    /// </summary>
    public static ColorPalette DarkPalette => new()
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
    /// High-contrast theme meeting WCAG AA, still cyan-led.
    /// </summary>
    public static ColorPalette HighContrastPalette => new()
    {
        Primary = "#006660",
        Secondary = "#003333",
        Tertiary = "#008888",
        Info = "#006660",
        Success = "#008800",
        Warning = "#CC6600",
        Error = "#CC0000",
        Background = "#FFFFFF",
        Surface = "#FFFFFF",
        TextPrimary = "#000000",
        TextSecondary = "#333333",
        Divider = "#666666",
        Header = "#FFFFFF",
        OptionBackground = "#F0F0F0",
        OnPrimary = "#FFFFFF"
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
    /// Border radius — sharp corners (0) for the neon stage language.
    /// </summary>
    public const int DefaultBorderRadius = 0;

    /// <summary>
    /// Gets the border radius as a pixel string.
    /// </summary>
    public static string BorderRadius => $"{DefaultBorderRadius}px";
}
