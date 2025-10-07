using System.Diagnostics;
namespace Nuotti.Contracts.Tests;

public class DocsLinkLintTests
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
    public void Markdown_links_are_valid()
    {
        var root = FindRepoRoot();
        var script = Path.Combine(root, "tools", "check-docs.ps1");
        Assert.True(File.Exists(script), $"Script not found: {script}");

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-File \"{script}\"",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            var message = $"Doc link check failed with exit code {p.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}";
            Assert.True(false, message);
        }
    }
}
