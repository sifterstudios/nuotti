# Font Resources

This directory contains embedded font files for the Nuotti Projector application.

## Font Loading Strategy

The application uses a comprehensive font fallback chain:

### Primary Font Stack
1. **Inter** (via Avalonia.Fonts.Inter package)
2. **Segoe UI** (Windows system font)
3. **SF Pro Display** (macOS system font)
4. **Ubuntu** (Linux system font)
5. **Arial** (Universal fallback)
6. **sans-serif** (CSS fallback)

### Monospace Font Stack
1. **JetBrains Mono** (if available)
2. **Fira Code** (if available)
3. **Consolas** (Windows)
4. **Monaco** (macOS)
5. **Courier New** (Universal)
6. **monospace** (CSS fallback)

## Usage

Fonts are automatically loaded by the `FontService` during application startup. The service provides:

- **Primary Font**: For body text and UI elements
- **Monospace Font**: For debug overlay and technical displays
- **Display Font**: For headers and emphasis

## Adding Custom Fonts

To add custom fonts:

1. Place `.ttf` or `.otf` files in this directory
2. The `FontService` will automatically detect and load them
3. Update the font fallback chains in `FontService.cs` if needed

## Performance

- Fonts are loaded asynchronously during startup
- System fonts are used as fallbacks to prevent layout shift
- Font loading failures gracefully degrade to system defaults
