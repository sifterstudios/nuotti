# Nuotti Projector - Theming Guide

## Overview

The Nuotti Projector desktop application now features the same vibrant, Kahoot/Bandle-inspired color palette as the Audience and Performer apps, with full light/dark mode support using Avalonia's theming system!

## 🎨 Color Palette

### Light Mode
- **Primary**: `#FF6B35` - Vibrant orange
- **Secondary**: `#004E89` - Deep blue
- **Tertiary**: `#1B9AAA` - Teal
- **Success**: `#46B283` - Green (for highlighting leaders)
- **Background**: `#FAFAFA` - Light gray
- **Surface**: `#FFFFFF` - White
- **Text Primary**: `#1A1A1A` - Almost black
- **Text Secondary**: `#666666` - Gray

### Dark Mode
- **Primary**: `#FF8C61` - Softer orange
- **Secondary**: `#2E7DAF` - Lighter blue
- **Tertiary**: `#48C9B0` - Brighter teal
- **Success**: `#5EC99D` - Lighter green
- **Background**: `#0A0E27` - Very dark blue-black (Bandle-inspired)
- **Surface**: `#151B3B` - Dark blue surface
- **Text Primary**: `#E8E8E8` - Light gray
- **Text Secondary**: `#B0B0B0` - Medium gray

## ✨ Features

### Theme Toggle
- **Header Button**: Click the sun/moon icon in the top right to toggle themes
- **Instant Switching**: Theme changes apply immediately to all UI elements
- **Smart Icons**: Moon icon for light mode, sun icon for dark mode
- **Keyboard Shortcut**: Press `Ctrl+T` to toggle (coming soon!)

### UI Improvements
- **Dynamic Resources**: All colors use Avalonia's ThemeDictionaries for seamless switching
- **Rounded Corners**: Modern 12px border radius on cards
- **Visual Hierarchy**: Clear separation between header, content, and log panel
- **Emoji Branding**: 🎵 icon in header and footer for personality
- **Improved Contrast**: Text and backgrounds optimized for readability

### Real-time Features
- **Answer Highlighting**: Leading answers highlighted in success green
- **Session Code**: Prominently displayed in primary color
- **Connection Status**: Clear indication of hub connection state
- **Debug Log**: Enhanced with better formatting and themed colors

## 🏗️ Architecture

### Theme Resources
Defined in `App.axaml` using Avalonia's ThemeDictionaries:

```xml
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
        <Color x:Key="PrimaryColor">#FF6B35</Color>
        <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />
        <!-- ... more colors -->
    </ResourceDictionary>
    
    <ResourceDictionary x:Key="Dark">
        <Color x:Key="PrimaryColor">#FF8C61</Color>
        <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />
        <!-- ... more colors -->
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

### Dynamic Resource Usage
All UI elements use `DynamicResource` for automatic theme switching:

```xml
<Border Background="{DynamicResource HeaderBrush}">
    <TextBlock Foreground="{DynamicResource PrimaryBrush}" />
</Border>
```

### Theme Toggle Implementation
In `MainWindow.axaml.cs`:

```csharp
private void ToggleTheme(object? sender, RoutedEventArgs e)
{
    var currentTheme = Application.Current?.ActualThemeVariant;
    var newTheme = currentTheme == ThemeVariant.Dark 
        ? ThemeVariant.Light 
        : ThemeVariant.Dark;
    
    Application.Current.RequestedThemeVariant = newTheme;
    UpdateThemeToggleButton();
}
```

## 🎯 Design Principles

1. **Consistency**: Matches color palette across all Nuotti applications
2. **Accessibility**: High contrast ratios for projector/presentation use
3. **Clarity**: Large text and clear visual hierarchy for audience viewing
4. **Professional**: Clean, modern design suitable for live performances
5. **Performance**: Efficient rendering for smooth real-time updates

## 🚀 Usage

### Running the Application
```bash
cd Nuotti.Projector
dotnet run
```

The application starts with the system's default theme (light or dark).

### Keyboard Shortcuts (Coming Soon)
- `Ctrl+T` - Toggle theme
- `F11` - Toggle fullscreen
- `Ctrl+L` - Toggle debug log panel

## 📋 Files Updated

### XAML
- `App.axaml` - Added theme resources with ThemeDictionaries
- `MainWindow.axaml` - Updated to use dynamic resources, added theme toggle button

### Code
- `MainWindow.axaml.cs` - Added theme toggle logic and dynamic resource lookups
- Added necessary using statements for Avalonia theming

## 🎨 UI Layout

```
┌────────────────────────────────────────────────────┐
│  🎵 SESSION CODE      [Connected]           🌙    │ Header
├──────────────────────────┬─────────────────────────┤
│                          │  📋 Debug Log           │
│  Question Text           ├─────────────────────────┤
│                          │                         │
│  ┌─────────────────┐    │  [Log entries...]       │
│  │ Choice A     42 │    │                         │
│  └─────────────────┘    │                         │
│  ┌─────────────────┐    │                         │
│  │ Choice B     18 │    │                         │
│  └─────────────────┘    │                         │
│                          │                         │
├──────────────────────────┴─────────────────────────┤
│          🎵 Nuotti Projector                       │ Footer
└────────────────────────────────────────────────────┘
```

## 🔮 Future Enhancements

- [ ] Fullscreen mode for presentations
- [ ] Hide/show debug log panel toggle
- [ ] Custom background images/gradients
- [ ] Animation effects for answer tallying
- [ ] Confetti effect for correct answers
- [ ] QR code display for session joining
- [ ] Transition animations between questions

## 🎓 Technical Notes

### Avalonia Theme System
Avalonia uses a variant-based theming system:
- `ThemeVariant.Light` - Light theme
- `ThemeVariant.Dark` - Dark theme
- `ThemeVariant.Default` - Follows system preference

### Dynamic Resources
Resources defined in ThemeDictionaries automatically switch when the theme changes. Use `DynamicResource` in XAML and `TryFindResource()` in code.

### Color Consistency
All colors are defined once in `App.axaml` and referenced throughout the application, ensuring consistency and easy maintenance.

---

**Built with ❤️ using Avalonia UI and .NET 9**

