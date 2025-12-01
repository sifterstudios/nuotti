using FluentAssertions;
using Nuotti.Contracts.V1.Design;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Design;

public class ContrastAuditTests
{
    [Fact]
    public void LightTheme_TextPrimary_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.LightPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"TextPrimary on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void LightTheme_TextPrimary_On_Surface_MeetsWCAGAA()
    {
        var palette = DesignTokens.LightPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Surface);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"TextPrimary on Surface should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void DarkTheme_TextPrimary_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.DarkPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"TextPrimary on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void DarkTheme_TextPrimary_On_Surface_MeetsWCAGAA()
    {
        var palette = DesignTokens.DarkPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Surface);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"TextPrimary on Surface should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_TextPrimary_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"HighContrast TextPrimary on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_TextPrimary_On_Surface_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Surface);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"HighContrast TextPrimary on Surface should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_TextSecondary_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextSecondary, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"HighContrast TextSecondary on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void LightTheme_PrimaryButton_Text_On_Primary_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.LightPalette;
        // For primary buttons, text should be white/light on primary color
        // Using white (#FFFFFF) as typical button text color
        var contrastRatio = ContrastCalculator.CalculateContrastRatio("#FFFFFF", palette.Primary);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"White text on Primary background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void DarkTheme_PrimaryButton_Text_On_Primary_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.DarkPalette;
        // For primary buttons, text should be white/light on primary color
        var contrastRatio = ContrastCalculator.CalculateContrastRatio("#FFFFFF", palette.Primary);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"White text on Primary background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_PrimaryButton_Text_On_Primary_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        // For primary buttons, text should be white/light on primary color
        var contrastRatio = ContrastCalculator.CalculateContrastRatio("#FFFFFF", palette.Primary);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"White text on Primary background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void LightTheme_Error_Text_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.LightPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Error, palette.Background);
        
        // Error colors should meet AA for large text at minimum (3:1)
        ContrastCalculator.MeetsWCAGAALarge(contrastRatio).Should().BeTrue(
            $"Error color on Background should meet WCAG AA for large text (3:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void LightTheme_Success_Text_On_Background_MeetsWCAGAALarge()
    {
        var palette = DesignTokens.LightPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Success, palette.Background);
        
        ContrastCalculator.MeetsWCAGAALarge(contrastRatio).Should().BeTrue(
            $"Success color on Background should meet WCAG AA for large text (3:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void DarkTheme_Error_Text_On_Background_MeetsWCAGAALarge()
    {
        var palette = DesignTokens.DarkPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Error, palette.Background);
        
        ContrastCalculator.MeetsWCAGAALarge(contrastRatio).Should().BeTrue(
            $"Error color on Background should meet WCAG AA for large text (3:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void DarkTheme_Success_Text_On_Background_MeetsWCAGAALarge()
    {
        var palette = DesignTokens.DarkPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Success, palette.Background);
        
        ContrastCalculator.MeetsWCAGAALarge(contrastRatio).Should().BeTrue(
            $"Success color on Background should meet WCAG AA for large text (3:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_Error_Text_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Error, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"HighContrast Error color on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void HighContrastTheme_Success_Text_On_Background_MeetsWCAGAA()
    {
        var palette = DesignTokens.HighContrastPalette;
        var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.Success, palette.Background);
        
        ContrastCalculator.MeetsWCAGAA(contrastRatio).Should().BeTrue(
            $"HighContrast Success color on Background should meet WCAG AA (4.5:1), but got {contrastRatio:F2}:1");
    }
    
    [Fact]
    public void AllThemes_TextSecondary_On_Surface_MeetsWCAGAALarge()
    {
        foreach (var variant in Enum.GetValues<ThemeVariant>())
        {
            var palette = DesignTokens.GetPalette(variant);
            var contrastRatio = ContrastCalculator.CalculateContrastRatio(palette.TextSecondary, palette.Surface);
            
            ContrastCalculator.MeetsWCAGAALarge(contrastRatio).Should().BeTrue(
                $"{variant} theme TextSecondary on Surface should meet WCAG AA for large text (3:1), but got {contrastRatio:F2}:1");
        }
    }
    
    [Fact]
    public void HighContrastTheme_Has_MaximumContrast()
    {
        var highContrast = DesignTokens.HighContrastPalette;
        var light = DesignTokens.LightPalette;
        var dark = DesignTokens.DarkPalette;
        
        // High contrast should have better contrast ratios than light/dark themes
        var hcTextBg = ContrastCalculator.CalculateContrastRatio(highContrast.TextPrimary, highContrast.Background);
        var lightTextBg = ContrastCalculator.CalculateContrastRatio(light.TextPrimary, light.Background);
        var darkTextBg = ContrastCalculator.CalculateContrastRatio(dark.TextPrimary, dark.Background);
        
        hcTextBg.Should().BeGreaterThan(lightTextBg,
            "High contrast theme should have better text/background contrast than light theme");
        hcTextBg.Should().BeGreaterThan(darkTextBg,
            "High contrast theme should have better text/background contrast than dark theme");
    }
}
