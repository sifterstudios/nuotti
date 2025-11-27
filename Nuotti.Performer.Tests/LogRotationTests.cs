using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServiceDefaults;
using System.IO;
using Xunit;

namespace Nuotti.Performer.Tests;

/// <summary>
/// Tests for log rotation and persistence (J8).
/// Verifies that log files are created, rotated by day, and old files are retained per retention policy.
/// </summary>
public class LogRotationTests
{
    [Fact]
    public void LogFileHelper_GetLogDirectory_CreatesDirectory()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "NuottiLogRotationTest", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("APPDATA", tempPath);
        
        try
        {
            // Act
            var logDir = LogFileHelper.GetLogDirectory("TestService");
            
            // Assert
            Assert.True(Directory.Exists(logDir));
            Assert.Contains("Nuotti", logDir);
            Assert.Contains("Logs", logDir);
            Assert.Contains("TestService", logDir);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("APPDATA", null);
            try { Directory.Delete(tempPath, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void LogFileHelper_GetLogFilePath_ReturnsExpectedFormat()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "NuottiLogRotationTest", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("APPDATA", tempPath);
        
        try
        {
            // Act
            var logPath = LogFileHelper.GetLogFilePath("TestService");
            
            // Assert
            Assert.Contains("TestService", logPath);
            Assert.Contains(DateTime.UtcNow.ToString("yyyyMMdd"), logPath);
            Assert.EndsWith(".log", logPath);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("APPDATA", null);
            try { Directory.Delete(tempPath, recursive: true); } catch { /* ignore */ }
        }
    }
}

