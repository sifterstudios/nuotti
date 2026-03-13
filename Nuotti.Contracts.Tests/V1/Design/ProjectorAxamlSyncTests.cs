using System.Xml.Linq;
using Nuotti.Contracts.V1.Design;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Design;

/// <summary>
/// Verifies that the color values hardcoded in Nuotti.Projector/App.axaml
/// stay in sync with the canonical DesignTokens in Nuotti.Contracts.
/// Avalonia cannot reference C# constants from XAML, so the values are
/// duplicated there — these tests catch any drift.
/// Note: Projector only supports Light and Dark (no HighContrast — it's a TV display app).
/// </summary>
public class ProjectorAxamlSyncTests
{
    [Fact]
    public void LightTheme_ColorsMatchDesignTokens()
        => AssertThemeMatches("Light", DesignTokens.LightPalette);

    [Fact]
    public void DarkTheme_ColorsMatchDesignTokens()
        => AssertThemeMatches("Dark", DesignTokens.DarkPalette);

    // -------------------------------------------------------------------------

    private static void AssertThemeMatches(string themeKey, ColorPalette palette)
    {
        var dict = LoadThemeDictionary(themeKey);

        AssertColor(dict, themeKey, "PrimaryColor",      palette.Primary);
        AssertColor(dict, themeKey, "SecondaryColor",    palette.Secondary);
        AssertColor(dict, themeKey, "TertiaryColor",     palette.Tertiary);
        AssertColor(dict, themeKey, "InfoColor",         palette.Info);
        AssertColor(dict, themeKey, "SuccessColor",      palette.Success);
        AssertColor(dict, themeKey, "WarningColor",      palette.Warning);
        AssertColor(dict, themeKey, "ErrorColor",        palette.Error);
        AssertColor(dict, themeKey, "BackgroundColor",   palette.Background);
        AssertColor(dict, themeKey, "SurfaceColor",      palette.Surface);
        AssertColor(dict, themeKey, "TextPrimaryColor",  palette.TextPrimary);
        AssertColor(dict, themeKey, "TextSecondaryColor",palette.TextSecondary);
        AssertColor(dict, themeKey, "DividerColor",      palette.Divider);
    }

    private static XElement LoadThemeDictionary(string themeKey)
    {
        var axaml = LoadAxaml();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var dict = axaml.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary"
                     && e.Attribute(x + "Key")?.Value == themeKey)
            .FirstOrDefault();

        Assert.True(dict != null,
            $"Could not find ThemeDictionary with x:Key=\"{themeKey}\" in App.axaml");
        return dict!;
    }

    private static void AssertColor(XElement dict, string theme, string key, string expectedHex)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var el = dict.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Color"
                              && e.Attribute(x + "Key")?.Value == key);

        Assert.True(el != null,
            $"[{theme}] Color '{key}' not found in App.axaml");

        var actual   = el!.Value.Trim().ToUpperInvariant();
        var expected = expectedHex.ToUpperInvariant();

        Assert.True(actual == expected,
            $"[{theme}] {key}: App.axaml has '{actual}' but DesignTokens says '{expected}'");
    }

    private static XDocument LoadAxaml()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Nuotti.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.True(dir != null, "Could not locate solution root from AppContext.BaseDirectory");

        var path = Path.Combine(dir!, "Nuotti.Projector", "App.axaml");
        Assert.True(File.Exists(path), $"App.axaml not found at expected path: {path}");

        return XDocument.Load(path);
    }
}
