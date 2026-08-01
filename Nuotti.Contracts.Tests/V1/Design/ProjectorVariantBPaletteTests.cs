using FluentAssertions;
using Nuotti.Contracts.V1.Design;
using Xunit;

namespace Nuotti.Contracts.Tests.V1.Design;

public class ProjectorVariantBPaletteTests
{
    [Fact]
    public void Variant_B_uses_accessible_cyan_primary_on_dark_stage()
    {
        var palette = DesignTokens.ProjectorVariantBPalette;
        palette.Primary.Should().Be("#00FFF5");
        palette.Background.Should().Be("#05090B");
        palette.Surface.Should().Be("#091115");
        palette.OnPrimary.Should().Be("#001413");

        ContrastCalculator.MeetsWCAGAA(
            ContrastCalculator.CalculateContrastRatio(palette.TextPrimary, palette.Background))
            .Should().BeTrue();
        ContrastCalculator.MeetsWCAGAA(
            ContrastCalculator.CalculateContrastRatio(palette.Primary, palette.Background))
            .Should().BeTrue("cyan accent on dark stage must remain accessible");
        ContrastCalculator.MeetsWCAGAA(
            ContrastCalculator.CalculateContrastRatio(palette.OnPrimary, palette.Primary))
            .Should().BeTrue("button label on cyan must meet AA (dark ink, not white)");
    }
}
