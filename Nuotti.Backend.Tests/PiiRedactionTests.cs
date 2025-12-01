using ServiceDefaults;
namespace Nuotti.Backend.Tests;

/// <summary>
/// Tests for PII redaction (J7).
/// Verifies that player names, file paths, and audience IDs are properly redacted.
/// </summary>
public class PiiRedactionTests
{
    [Fact]
    public void RedactPlayerName_TruncatesCorrectly()
    {
        var redacted = PiiRedactor.RedactPlayerName("John Doe");
        Assert.StartsWith("J", redacted);
        Assert.Contains("*", redacted);
        
        var shortName = PiiRedactor.RedactPlayerName("A");
        Assert.Equal("A***", shortName);
    }

    [Fact]
    public void RedactPlayerName_NullOrEmpty_ReturnsAnonymous()
    {
        Assert.Equal("anonymous", PiiRedactor.RedactPlayerName(null));
        Assert.Equal("anonymous", PiiRedactor.RedactPlayerName(""));
        Assert.Equal("anonymous", PiiRedactor.RedactPlayerName("   "));
    }

    [Fact]
    public void RedactFilePath_ExtractsBasename()
    {
        var redacted = PiiRedactor.RedactFilePath(@"C:\Users\John\Documents\song.mp3");
        Assert.Equal("song.mp3", redacted);
        
        var unixPath = PiiRedactor.RedactFilePath("/home/user/music/track.wav");
        Assert.Equal("track.wav", unixPath);
    }

    [Fact]
    public void RedactFilePath_LongFileName_UsesHash()
    {
        var longPath = PiiRedactor.RedactFilePath(new string('a', 60) + ".mp3");
        // Should return a hash (8 hex characters)
        Assert.True(longPath.Length <= 12); // hash + extension or just hash
    }

    [Fact]
    public void RedactAudienceId_Guid_Truncates()
    {
        var guid = Guid.NewGuid().ToString();
        var redacted = PiiRedactor.RedactAudienceId(guid);
        Assert.StartsWith(guid.Substring(0, 8), redacted);
        Assert.EndsWith("***", redacted);
    }

    [Fact]
    public void RedactAudienceId_NonGuid_Hashes()
    {
        var id = "audience-123";
        var redacted = PiiRedactor.RedactAudienceId(id);
        // Should return a hash (8 hex characters)
        Assert.Equal(8, redacted.Length);
    }
}

