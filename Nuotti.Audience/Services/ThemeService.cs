using Microsoft.JSInterop;
using MudBlazor;
using Nuotti.Contracts.V1.Design;
namespace Nuotti.Audience.Services;

public class ThemeService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ThemeService>? _objRef;
    
    public bool IsDarkMode { get; private set; }
    public ThemeVariant CurrentVariant { get; private set; } = ThemeVariant.Light;
    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
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
            // Use system preference
            IsDarkMode = await _jsRuntime.InvokeAsync<bool>("matchMedia", "(prefers-color-scheme: dark)");
            CurrentVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        
        // Setup listener for system theme changes
        await _jsRuntime.InvokeVoidAsync("nuottiTheme.initialize", _objRef);
    }

    public async Task ToggleThemeAsync()
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
        var preference = CurrentVariant.ToString().ToLowerInvariant();
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme-preference", preference);
        OnThemeChanged?.Invoke();
    }

    [JSInvokable]
    public void SystemThemeChanged(bool isDark)
    {
        // Only respond if user hasn't set a preference
        var task = Task.Run(async () =>
        {
            var savedPreference = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "theme-preference");
            if (string.IsNullOrEmpty(savedPreference))
            {
                IsDarkMode = isDark;
                CurrentVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
                OnThemeChanged?.Invoke();
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
            // Use default Typography to ensure compatibility with current MudBlazor version
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
        if (_objRef != null)
        {
            await _jsRuntime.InvokeVoidAsync("nuottiTheme.dispose");
            _objRef.Dispose();
        }
    }
}

