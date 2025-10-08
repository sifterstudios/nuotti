using System.Diagnostics;
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

    [Fact]
    public void Example_help_command_runs_locally()
    {
        var root = FindRepoRoot();
        var project = Path.Combine(root, "Nuotti.SimKit", "Nuotti.SimKit.csproj");
        Assert.True(File.Exists(project), $"Missing project: {project}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{project}\" -- --help",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);

        Assert.True(p.HasExited, "dotnet run did not exit within 60s");
        if (p.ExitCode != 0)
        {
            var message = $"SimKit help example failed with exit code {p.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}";
            Assert.True(false, message);
        }
        Assert.Contains("Usage:", stdout);
        Assert.Contains("--help", stdout);
    }
}
