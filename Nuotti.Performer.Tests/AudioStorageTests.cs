using System.Text;
using Xunit;
namespace Nuotti.Performer.Tests;

public class AudioStorageTests
{
    [Fact]
    public async Task ComputeSha256HexAsync_KnownValue()
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var hex = await AudioStorage.ComputeSha256HexAsync(ms);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hex);
    }

    [Fact]
    public void GetBlobPath_ComputesUnderAppData()
    {
        var fileName = "song.mp3";
        var hash = new string('a', 64);
        var path = AudioStorage.GetBlobPath(hash, fileName);
        // Should end with ...\\Nuotti\\Audio\\{hash}\\{filename}
        Assert.EndsWith($"Nuotti{Path.DirectorySeparatorChar}Audio{Path.DirectorySeparatorChar}{hash}{Path.DirectorySeparatorChar}{fileName}", path);
    }
}
