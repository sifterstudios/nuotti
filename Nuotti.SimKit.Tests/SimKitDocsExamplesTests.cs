using Xunit;
namespace Nuotti.SimKit.Tests;

public class SimKitDocsExamplesTests
{
    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Nuotti.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void Docs_simkit_exists_and_has_sections()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "simkit.md");
        Assert.True(File.Exists(path), $"Missing docs/simkit.md at {path}");
        var text = File.ReadAllText(path);
        Assert.Contains("## Quickstart", text);
        Assert.Contains("## Presets", text);
        Assert.Contains("## Writing scenarios", text);
        Assert.Contains("## Reading reports", text);
        Assert.Contains("## Examples", text);
    }
}
