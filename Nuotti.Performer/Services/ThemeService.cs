using MudBlazor;
namespace Nuotti.Performer.Services;

public class ThemeService
{
    private bool _isDarkMode;
    
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

    public event Action? OnThemeChanged;

    public ThemeService()
    {
        // Default to system preference (we could enhance this later)
        _isDarkMode = false;
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    public void SetTheme(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
    }

    public MudTheme GetTheme()
    {
        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#FF6B35",        // Vibrant orange (Kahoot-inspired)
                Secondary = "#004E89",      // Deep blue
                Tertiary = "#1B9AAA",       // Teal
                Info = "#06BEE1",           // Bright cyan
                Success = "#46B283",        // Green
                Warning = "#F77F00",        // Amber
                Error = "#EF476F",          // Pink-red
                Background = "#FAFAFA",     // Light gray
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                DrawerBackground = "#FFFFFF",
                TextPrimary = "#1A1A1A",
                TextSecondary = "#666666",
                ActionDefault = "#1A1A1A",
                Divider = "#E0E0E0",
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#FF8C61",        // Softer orange for dark mode
                Secondary = "#2E7DAF",      // Lighter blue
                Tertiary = "#48C9B0",       // Brighter teal
                Info = "#3DD9FF",           // Lighter cyan
                Success = "#5EC99D",        // Lighter green
                Warning = "#FFA040",        // Lighter amber
                Error = "#FF6B93",          // Lighter pink-red
                Background = "#0A0E27",     // Very dark blue-black (Bandle-inspired)
                Surface = "#151B3B",        // Dark blue surface
                AppbarBackground = "#151B3B",
                DrawerBackground = "#0A0E27",
                TextPrimary = "#E8E8E8",
                TextSecondary = "#B0B0B0",
                ActionDefault = "#E8E8E8",
                Divider = "#2A2F4F",
            },
            Typography = new Typography(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px",
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
}

