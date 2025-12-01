using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Nuotti.Contracts.V1.Design;
using System.Linq;

namespace Nuotti.Projector.Services;

/// <summary>
/// Helper service for managing theme variants including HighContrast,
/// which isn't natively supported by Avalonia's ThemeVariant enum.
/// </summary>
public static class ThemeHelper
{
    private const string HighContrastKey = "HighContrast";
    
    /// <summary>
    /// Applies the specified theme variant to the application.
    /// </summary>
    public static void ApplyThemeVariant(ThemeVariant variant)
    {
        if (Application.Current == null) return;
        
        switch (variant)
        {
            case Contracts.V1.Design.ThemeVariant.Light:
                Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                RemoveHighContrastOverrides();
                break;
                
            case Contracts.V1.Design.ThemeVariant.Dark:
                Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                RemoveHighContrastOverrides();
                break;
                
            case Contracts.V1.Design.ThemeVariant.HighContrast:
                // HighContrast uses Light as base, then overrides resources
                Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                ApplyHighContrastOverrides();
                break;
        }
    }
    
    /// <summary>
    /// Gets the current theme variant from application state.
    /// </summary>
    public static ThemeVariant GetCurrentThemeVariant()
    {
        if (Application.Current == null)
            return ThemeVariant.Light;
        
        // Check if HighContrast overrides are active
        if (IsHighContrastActive())
            return ThemeVariant.HighContrast;
        
        var avaloniaVariant = Application.Current.ActualThemeVariant;
        return avaloniaVariant == Avalonia.Styling.ThemeVariant.Dark 
            ? ThemeVariant.Dark 
            : ThemeVariant.Light;
    }
    
    /// <summary>
    /// Cycles through theme variants: Light -> Dark -> HighContrast -> Light
    /// </summary>
    public static ThemeVariant GetNextThemeVariant(ThemeVariant current)
    {
        return current switch
        {
            ThemeVariant.Light => ThemeVariant.Dark,
            ThemeVariant.Dark => ThemeVariant.HighContrast,
            ThemeVariant.HighContrast => ThemeVariant.Light,
            _ => ThemeVariant.Light
        };
    }
    
    private static bool IsHighContrastActive()
    {
        if (Application.Current?.Resources == null) return false;
        
        // Check if HighContrast marker exists in merged dictionaries
        return Application.Current.Resources.MergedDictionaries
            .Any(dict => dict.ContainsKey("HighContrastActive"));
    }
    
    private static void ApplyHighContrastOverrides()
    {
        if (Application.Current?.Resources == null) return;
        
        var themeDict = Application.Current.Resources.ThemeDictionaries;
        if (!themeDict.ContainsKey(HighContrastKey))
            return;
        
        var highContrastDict = themeDict[HighContrastKey] as ResourceDictionary;
        if (highContrastDict == null) return;
        
        // Mark that HighContrast is active
        var markerDict = new ResourceDictionary
        {
            ["HighContrastActive"] = true
        };
        
        // Merge HighContrast resources into main resources
        // This will override Light theme resources
        foreach (var kvp in highContrastDict)
        {
            if (Application.Current.Resources.ContainsKey(kvp.Key))
            {
                Application.Current.Resources[kvp.Key] = kvp.Value;
            }
            else
            {
                Application.Current.Resources.Add(kvp.Key, kvp.Value);
            }
        }
        
        Application.Current.Resources.MergedDictionaries.Add(markerDict);
    }
    
    private static void RemoveHighContrastOverrides()
    {
        if (Application.Current?.Resources == null) return;
        
        // Remove HighContrast marker
        var markerDict = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(dict => dict.ContainsKey("HighContrastActive"));
        
        if (markerDict != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(markerDict);
        }
        
        // Reset resources by reloading theme dictionaries
        // Note: This is a simplified approach - in practice, you'd want to restore
        // original resource values. For now, switching theme variant will reload them.
        if (Application.Current.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Light)
        {
            var themeDict = Application.Current.Resources.ThemeDictionaries;
            if (themeDict.ContainsKey("Light"))
            {
                var lightDict = themeDict["Light"] as ResourceDictionary;
                if (lightDict != null)
                {
                    foreach (var kvp in lightDict)
                    {
                        if (Application.Current.Resources.ContainsKey(kvp.Key))
                        {
                            Application.Current.Resources[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Gets a resource value, checking HighContrast overrides first if active.
    /// </summary>
    public static bool TryGetResource(string key, out object? value)
    {
        value = null;
        
        if (Application.Current?.Resources == null)
            return false;
        
        // If HighContrast is active, check HighContrast dictionary first
        if (IsHighContrastActive())
        {
            var themeDict = Application.Current.Resources.ThemeDictionaries;
            if (themeDict.ContainsKey(HighContrastKey))
            {
                var highContrastDict = themeDict[HighContrastKey] as ResourceDictionary;
                if (highContrastDict?.TryGetResource(key, Application.Current.ActualThemeVariant, out value) == true)
                {
                    return true;
                }
            }
        }
        
        // Fall back to normal resource lookup
        return Application.Current.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out value);
    }
}
