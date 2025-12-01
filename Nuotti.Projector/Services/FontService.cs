using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media;

namespace Nuotti.Projector.Services;

public class FontService
{
    private readonly Dictionary<string, FontFamily> _loadedFonts = new();
    private bool _fontsLoaded = false;
    
    // Font fallback chain - from most preferred to system fallbacks
    private readonly string[] _fontFallbackChain = new[]
    {
        "Inter", // Primary font (modern, readable)
        "Segoe UI", // Windows fallback
        "SF Pro Display", // macOS fallback
        "Ubuntu", // Linux fallback
        "Arial", // Universal fallback
        "sans-serif" // CSS fallback
    };
    
    public FontFamily PrimaryFont { get; private set; }
    public FontFamily MonospaceFont { get; private set; }
    public FontFamily DisplayFont { get; private set; }
    
    public FontService()
    {
        // Initialize with system defaults until custom fonts are loaded
        PrimaryFont = FontFamily.Default;
        MonospaceFont = new FontFamily("Consolas, Monaco, 'Courier New', monospace");
        DisplayFont = FontFamily.Default;
    }
    
    public async Task LoadFontsAsync()
    {
        if (_fontsLoaded) return;
        
        try
        {
            // Try to load embedded fonts first
            await LoadEmbeddedFontsAsync();
            
            // Set up font families with fallbacks
            SetupFontFamilies();
            
            _fontsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Font loading failed, using system defaults: {ex.Message}");
            // Continue with system defaults
            _fontsLoaded = true;
        }
    }
    
    private async Task LoadEmbeddedFontsAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();
            
            foreach (var resourceName in resourceNames)
            {
                if (resourceName.EndsWith(".ttf") || resourceName.EndsWith(".otf"))
                {
                    await LoadFontFromResourceAsync(assembly, resourceName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading embedded fonts: {ex.Message}");
        }
    }
    
    private Task LoadFontFromResourceAsync(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return Task.CompletedTask;
            
            var fontName = Path.GetFileNameWithoutExtension(resourceName.Split('.')[^2]);
            
            // For now, we'll rely on system font loading
            // Avalonia's font loading from streams is complex and may require platform-specific code
            Console.WriteLine($"Font resource found: {fontName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading font {resourceName}: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    private void SetupFontFamilies()
    {
        // Create font families with comprehensive fallback chains
        PrimaryFont = CreateFontFamilyWithFallbacks(_fontFallbackChain);
        
        // Monospace fonts for debug/technical display
        var monospaceFallbacks = new[]
        {
            "JetBrains Mono", "Fira Code", "Consolas", "Monaco", 
            "Courier New", "monospace"
        };
        MonospaceFont = CreateFontFamilyWithFallbacks(monospaceFallbacks);
        
        // Display fonts for headers and emphasis
        var displayFallbacks = new[]
        {
            "Inter", "Segoe UI", "SF Pro Display", "Ubuntu", 
            "Helvetica Neue", "Arial", "sans-serif"
        };
        DisplayFont = CreateFontFamilyWithFallbacks(displayFallbacks);
    }
    
    private FontFamily CreateFontFamilyWithFallbacks(string[] fontNames)
    {
        var fallbackString = string.Join(", ", fontNames);
        return new FontFamily(fallbackString);
    }
    
    public FontFamily GetFontFamily(FontType fontType)
    {
        return fontType switch
        {
            FontType.Primary => PrimaryFont,
            FontType.Monospace => MonospaceFont,
            FontType.Display => DisplayFont,
            _ => PrimaryFont
        };
    }
    
    public bool AreFontsLoaded => _fontsLoaded;
    
    public void PreloadFonts()
    {
        // Trigger font loading by accessing font properties
        _ = PrimaryFont.Name;
        _ = MonospaceFont.Name;
        _ = DisplayFont.Name;
    }
    
    public string GetFontDiagnostics()
    {
        return $@"Font Service Diagnostics:
Primary: {PrimaryFont.Name}
Monospace: {MonospaceFont.Name}  
Display: {DisplayFont.Name}
Fonts Loaded: {_fontsLoaded}
Loaded Fonts Count: {_loadedFonts.Count}";
    }
}

public enum FontType
{
    Primary,
    Monospace,
    Display
}
