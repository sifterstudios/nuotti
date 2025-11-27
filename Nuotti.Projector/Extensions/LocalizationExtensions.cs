using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Nuotti.Projector.Services;

namespace Nuotti.Projector.Extensions;

/// <summary>
/// XAML markup extension for localized strings
/// Usage: Text="{loc:Localize Key=common.loading}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;
    public object[]? Args { get; set; }
    
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // For now, return a simple binding that can be updated later
        // In a full implementation, this would connect to the LocalizationService
        return new Binding($"[{Key}]") { Mode = BindingMode.OneWay };
    }
}

/// <summary>
/// Static helper class for localization in code-behind
/// </summary>
public static class LocalizationExtensions
{
    private static LocalizationService? _localizationService;
    
    public static void Initialize(LocalizationService localizationService)
    {
        _localizationService = localizationService;
    }
    
    public static string Localize(this string key, params object[] args)
    {
        return _localizationService?.GetString(key, args) ?? $"[{key}]";
    }
    
    public static string LocalizePlural(this string baseKey, int count, params object[] args)
    {
        return _localizationService?.GetPluralString(baseKey, count, args) ?? $"[{baseKey}]";
    }
}
