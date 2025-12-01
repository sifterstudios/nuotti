using Avalonia;
// Disambiguate theme types: our domain enum vs Avalonia's ThemeVariant
using AvaloniaThemeVariant = Avalonia.Styling.ThemeVariant;
using NuottiThemeVariant = Nuotti.Contracts.V1.Design.NuottiThemeVariant;
using System.Linq;

namespace Nuotti.Projector.Services;

/// <summary>
/// Helper service for managing theme variants including HighContrast,
/// which isn't natively supported by Avalonia's ThemeVariant enum.
/// </summary>
public static class ThemeHelper
{
    private const string HighContrastKey = "HighContrast"; // marker only; overrides are currently no-op

    /// <summary>
    /// Applies the specified theme variant to the application.
    /// </summary>
    public static void ApplyThemeVariant(NuottiThemeVariant variant)
    {
        if (Application.Current == null) return;

        switch (variant)
        {
            case NuottiThemeVariant.Light:
                Application.Current.RequestedThemeVariant = AvaloniaThemeVariant.Light;
                RemoveHighContrastOverrides();
                break;

            case NuottiThemeVariant.Dark:
                Application.Current.RequestedThemeVariant = AvaloniaThemeVariant.Dark;
                RemoveHighContrastOverrides();
                break;

            case NuottiThemeVariant.HighContrast:
                // HighContrast uses Light as base, then overrides resources
                Application.Current.RequestedThemeVariant = AvaloniaThemeVariant.Light;
                ApplyHighContrastOverrides();
                break;
        }
    }

    /// <summary>
    /// Gets the current theme variant from application state.
    /// </summary>
    public static NuottiThemeVariant GetCurrentThemeVariant()
    {
        if (Application.Current == null)
            return NuottiThemeVariant.Light;

        // Check if HighContrast overrides are active
        if (IsHighContrastActive())
            return NuottiThemeVariant.HighContrast;

        var avaloniaVariant = Application.Current.ActualThemeVariant;
        return avaloniaVariant == AvaloniaThemeVariant.Dark
            ? NuottiThemeVariant.Dark
            : NuottiThemeVariant.Light;
    }

    /// <summary>
    /// Cycles through theme variants: Light -> Dark -> HighContrast -> Light
    /// </summary>
    public static NuottiThemeVariant GetNextThemeVariant(NuottiThemeVariant current)
    {
        return current switch
        {
            NuottiThemeVariant.Light => NuottiThemeVariant.Dark,
            NuottiThemeVariant.Dark => NuottiThemeVariant.HighContrast,
            NuottiThemeVariant.HighContrast => NuottiThemeVariant.Light,
            _ => NuottiThemeVariant.Light
        };
    }

    private static bool IsHighContrastActive()
    {
        if (Application.Current?.Resources == null) return false;

        // Check if HighContrast marker exists in merged dictionaries (not currently used)
        return Application.Current.Resources.MergedDictionaries
            .Any(dict => dict.TryGetResource("HighContrastActive", Application.Current.ActualThemeVariant, out _));
    }

    private static void ApplyHighContrastOverrides()
    {
        // No-op for now. If we introduce a dedicated HighContrast resource dictionary,
        // merging would happen here.
    }

    private static void RemoveHighContrastOverrides()
    {
        // No-op for now.
    }

    /// <summary>
    /// Gets a resource value, checking HighContrast overrides first if active.
    /// </summary>
    public static bool TryGetResource(string key, out object? value)
    {
        value = null;

        if (Application.Current?.Resources == null)
            return false;

        // If HighContrast is active, we would check a dedicated dictionary first (not implemented).

        // Fall back to normal resource lookup
        return Application.Current.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out value);
    }
}
