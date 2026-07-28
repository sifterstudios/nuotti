using System;
using FluentAssertions;
using Nuotti.Projector.Presentation;
using Xunit;

namespace Nuotti.Projector.Tests;

public class PresentationAssemblyTests
{
    /// <summary>
    /// The presentation layer must be usable from the SimKit harness and from tests without a
    /// window. If Avalonia creeps back into this assembly, headless simulation breaks.
    /// </summary>
    [Fact]
    public void Presentation_assembly_does_not_reference_Avalonia()
    {
        var referenced = typeof(PhasePresenter).Assembly.GetReferencedAssemblies();

        referenced.Should().NotContain(
            a => a.Name != null && a.Name.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowSize_carries_width_and_height()
    {
        var size = new WindowSize(1920, 1080);

        size.Width.Should().Be(1920);
        size.Height.Should().Be(1080);
    }
}
