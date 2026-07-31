using Avalonia.Collections;
using FluentAssertions;
using Nuotti.Projector.Services;
using Xunit;

namespace Nuotti.Projector.Tests;

public class LogRetentionTests
{
    [Fact]
    public void Append_keeps_the_newest_rows_in_chronological_order()
    {
        var rows = new AvaloniaList<string>();

        for (var index = 1; index <= 205; index++)
        {
            LogRetention.Append(rows, $"row-{index}", maximumRows: 200);
        }

        rows.Should().HaveCount(200);
        rows[0].Should().Be("row-6");
        rows[^1].Should().Be("row-205");
    }
}
