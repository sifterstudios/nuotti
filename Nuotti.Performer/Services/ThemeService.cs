using Microsoft.JSInterop;
using MudBlazor;
using Nuotti.Contracts.V1.Design;
namespace Nuotti.Performer.Services;

public class ThemeService : IAsyncDisposable
{
    private readonly IJSRuntime? _jsRuntime;
    private DotNetObjectReference<ThemeService>? _objRef;
    private bool _isDarkMode;
    private ThemeVariant _currentVariant = ThemeVariant.Light;
    
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                OnThemeChanged?.Invoke();
            }
        }
    }
    
    public ThemeVariant CurrentVariant
    {
        get => _currentVariant;
        private set
        {
            if (_currentVariant != value)
            {
                _currentVariant = value;
                OnThemeChanged?.Invoke();
            }
        }
    }

    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime? jsRuntime = null)
    {
        _jsRuntime = jsRuntime;
        // Default to light mode - will be updated in InitializeAsync if JS runtime is available
        _isDarkMode = false;
    }
    
    public async Task InitializeAsync(IJSRuntime? jsRuntime = null)
    {
        var runtime = jsRuntime ?? _jsRuntime;
        if (runtime == null) return;
        
        _objRef = DotNetObjectReference.Create(this);
        
        // Check for saved preference, otherwise use system preference
        var savedPreference = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "theme-preference");
        
        if (!string.IsNullOrEmpty(savedPreference))
        {
            CurrentVariant = savedPreference switch
            {
                "light" => ThemeVariant.Light,
                "dark" => ThemeVariant.Dark,
                "highcontrast" => ThemeVariant.HighContrast,
                _ => ThemeVariant.Light
            };
            // HighContrast uses light theme base (white background)
            IsDarkMode = CurrentVariant == ThemeVariant.Dark;
        }
        else
        {
            // Use system preference if available
            try
            {
                var prefersDark = await runtime.InvokeAsync<bool>("matchMedia", "(prefers-color-scheme: dark)");
                IsDarkMode = prefersDark;
                CurrentVariant = prefersDark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
            catch
            {
                // JS not available, use default
                IsDarkMode = false;
                CurrentVariant = ThemeVariant.Light;
            }
        }
        
        // Setup listener for system theme changes
        try
        {
            await runtime.InvokeVoidAsync("nuottiTheme.initialize", _objRef);
        }
        catch
        {
            // JS theme helper not available, continue without system theme detection
        }
    }

    public async Task ToggleThemeAsync(IJSRuntime? jsRuntime = null)
    {
        // Cycle through: Light -> Dark -> HighContrast -> Light
        CurrentVariant = CurrentVariant switch
        {
            ThemeVariant.Light => ThemeVariant.Dark,
            ThemeVariant.Dark => ThemeVariant.HighContrast,
            ThemeVariant.HighContrast => ThemeVariant.Light,
            _ => ThemeVariant.Light
        };
        
        // HighContrast uses light theme base (white background), so IsDarkMode is false
        IsDarkMode = CurrentVariant == ThemeVariant.Dark;
        
        var runtime = jsRuntime ?? _jsRuntime;
        if (runtime != null)
        {
            var preference = CurrentVariant.ToString().ToLowerInvariant();
            try
            {
                await runtime.InvokeVoidAsync("localStorage.setItem", "theme-preference", preference);
            }
            catch
            {
                // localStorage not available, continue
            }
        }
    }
    
    public void ToggleTheme()
    {
        // Synchronous version for backwards compatibility
        _ = Task.Run(async () => await ToggleThemeAsync());
    }

    public void SetTheme(bool isDarkMode)
    {
        CurrentVariant = isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        IsDarkMode = isDarkMode;
    }
    
    [JSInvokable]
    public void SystemThemeChanged(bool isDark)
    {
        // Only respond if user hasn't set a preference
        if (_jsRuntime == null) return;
        
        var task = Task.Run(async () =>
        {
            try
            {
                var runtime = _jsRuntime;
            if (runtime == null) return;
            var savedPreference = await runtime.InvokeAsync<string?>("localStorage.getItem", "theme-preference");
                if (string.IsNullOrEmpty(savedPreference))
                {
                    IsDarkMode = isDark;
                    CurrentVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
                }
            }
            catch
            {
                // JS not available, ignore
            }
        });
    }

    public MudTheme GetTheme()
    {
        // Use shared design tokens from Nuotti.Contracts
        var lightPalette = DesignTokens.LightPalette;
        var darkPalette = DesignTokens.DarkPalette;
        var highContrastPalette = DesignTokens.HighContrastPalette;
        
        // Determine which palettes to use based on current variant
        var lightPaletteToUse = CurrentVariant == ThemeVariant.HighContrast 
            ? highContrastPalette 
            : lightPalette;
        
        // Dark palette is always normal (HighContrast is light-based)
        var darkPaletteToUse = darkPalette;
        
        return new MudTheme
        {
            PaletteLight = CreatePaletteLight(lightPaletteToUse),
            PaletteDark = CreatePaletteDark(darkPaletteToUse),
            Typography = new Typography(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = DesignTokens.BorderRadius,
            },
            ZIndex = new ZIndex
            {
                Drawer = 1300,
                AppBar = 1200,
                Dialog = 1400,
                Popover = 1500,
                Snackbar = 1600,
                Tooltip = 1700
            }
        };
    }
    
    private static PaletteLight CreatePaletteLight(ColorPalette palette)
    {
        return new PaletteLight
        {
            Primary = palette.Primary,
            Secondary = palette.Secondary,
            Tertiary = palette.Tertiary,
            Info = palette.Info,
            Success = palette.Success,
            Warning = palette.Warning,
            Error = palette.Error,
            Background = palette.Background,
            Surface = palette.Surface,
            AppbarBackground = palette.Header ?? palette.Surface,
            DrawerBackground = palette.Surface,
            TextPrimary = palette.TextPrimary,
            TextSecondary = palette.TextSecondary,
            ActionDefault = palette.TextPrimary,
            Divider = palette.Divider,
        };
    }
    
    private static PaletteDark CreatePaletteDark(ColorPalette palette)
    {
        return new PaletteDark
        {
            Primary = palette.Primary,
            Secondary = palette.Secondary,
            Tertiary = palette.Tertiary,
            Info = palette.Info,
            Success = palette.Success,
            Warning = palette.Warning,
            Error = palette.Error,
            Background = palette.Background,
            Surface = palette.Surface,
            AppbarBackground = palette.Header ?? palette.Surface,
            DrawerBackground = palette.Background,
            TextPrimary = palette.TextPrimary,
            TextSecondary = palette.TextSecondary,
            ActionDefault = palette.TextPrimary,
            Divider = palette.Divider,
        };
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_objRef != null && _jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("nuottiTheme.dispose");
            }
            catch
            {
                // JS not available, continue
            }
            _objRef.Dispose();
        }
    }
}


