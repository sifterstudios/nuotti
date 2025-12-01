namespace Nuotti.Contracts.V1.Design;

/// <summary>
/// Theme variant options for Nuotti applications.
/// </summary>
public enum ThemeVariant
{
    /// <summary>
    /// Light theme variant.
    /// </summary>
    Light,

    /// <summary>
    /// Dark theme variant.
    /// </summary>
    Dark,

    /// <summary>
    /// High-contrast theme variant meeting WCAG AA accessibility standards.
    /// </summary>
    HighContrast
}

/// <summary>
/// Alias enum to disambiguate from Avalonia's ThemeVariant in app code.
/// Use this in UI apps to avoid type name collisions.
/// </summary>
public enum NuottiThemeVariant
{
    Light,
    Dark,
    HighContrast
}
