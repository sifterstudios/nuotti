# Nuotti Performer - Theming Guide

## Overview

The Nuotti Performer application now features the same vibrant, Kahoot/Bandle-inspired color palette as the Audience app, with full light/dark mode support!

## 🎨 Color Palette

### Light Mode
- **Primary**: `#FF6B35` - Vibrant orange
- **Secondary**: `#004E89` - Deep blue
- **Tertiary**: `#1B9AAA` - Teal
- **Info**: `#06BEE1` - Bright cyan
- **Success**: `#46B283` - Green
- **Warning**: `#F77F00` - Amber
- **Error**: `#EF476F` - Pink-red
- **Background**: `#FAFAFA` - Light gray
- **Surface**: `#FFFFFF` - White

### Dark Mode
- **Primary**: `#FF8C61` - Softer orange
- **Secondary**: `#2E7DAF` - Lighter blue
- **Tertiary**: `#48C9B0` - Brighter teal
- **Info**: `#3DD9FF` - Lighter cyan
- **Success**: `#5EC99D` - Lighter green
- **Warning**: `#FFA040` - Lighter amber
- **Error**: `#FF6B93` - Lighter pink-red
- **Background**: `#0A0E27` - Very dark blue-black
- **Surface**: `#151B3B` - Dark blue surface

## ✨ Features

### Theme Toggle
- **App Bar Button**: Click the sun/moon icon in the top right to toggle themes
- **Settings Menu**: Access theme toggle from the Settings menu in the drawer
- **Real-time Updates**: Theme changes apply instantly across all components

### UI Improvements
- Modern, rounded corners (12px border radius)
- Playful emoji icons in headers and titles
- Smooth theme transitions
- Consistent spacing and typography
- Elevated cards and surfaces

## 🏗️ Architecture

### ThemeService
Located in `Services/ThemeService.cs`, this singleton service manages the theme state:

```csharp
public class ThemeService
{
    public bool IsDarkMode { get; }
    public event Action? OnThemeChanged;
    
    public void ToggleTheme()
    public void SetTheme(bool isDarkMode)
    public MudTheme GetTheme()
}
```

**Usage:**
```csharp
@inject ThemeService ThemeService

protected override void OnInitialized()
{
    _theme = ThemeService.GetTheme();
    _isDarkMode = ThemeService.IsDarkMode;
    ThemeService.OnThemeChanged += OnThemeChanged;
}

private void ToggleTheme()
{
    ThemeService.ToggleTheme();
}
```

### MainLayout Integration
The MainLayout subscribes to theme changes and updates the UI accordingly:

```razor
<MudThemeProvider @ref="@_mudThemeProvider" 
                  Theme="@_theme" 
                  IsDarkMode="@_isDarkMode" />
```

## 🎯 Design Principles

1. **Consistency**: Same color palette across Audience, Performer, and Projector
2. **Accessibility**: High contrast ratios in both light and dark modes
3. **Playfulness**: Emoji icons and vibrant colors for a fun experience
4. **Professional**: Clean, modern design that's still production-ready

## 🚀 Usage

### Starting the Application
```bash
cd Nuotti.Performer
dotnet run
```

The application will start in light mode by default. Use the theme toggle to switch to dark mode.

### Customizing Colors
To modify the color palette, edit the `GetTheme()` method in `Services/ThemeService.cs`.

## 📋 Components Updated

- `Shared/MainLayout.razor` - Added theme toggle and updated styling
- `Services/ThemeService.cs` - New theme management service
- `Program.cs` - Registered ThemeService as singleton
- `_Imports.razor` - Added Services namespace

## 🔮 Future Enhancements

- [ ] Persist theme preference in local storage
- [ ] System theme detection
- [ ] Custom theme builder UI
- [ ] Per-user theme preferences
- [ ] Theme preview mode
- [ ] Additional color schemes (e.g., "Ocean", "Sunset")

---

**Built with ❤️ using Blazor Server and MudBlazor**


