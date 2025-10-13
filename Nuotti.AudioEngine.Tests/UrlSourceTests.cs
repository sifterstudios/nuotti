using FluentAssertions;
using System;
using System.IO;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class UrlSourceTests
{
    [Theory]
    [InlineData("http://example.com/a.mp3")]
    [InlineData("https://example.com/a.mp3")]
    public void TryNormalize_accepts_http_https(string input)
    {
        var ok = UrlSource.TryNormalize(input, out var parsed, out var error);
        ok.Should().BeTrue();
        parsed!.Should().NotBeNull();
        parsed!.Normalized.Should().Be(input);
        error.Should().BeNull();
    }

    [Fact]
    public void TryNormalize_accepts_file_uri_and_sets_local_path()
    {
        var tmp = Path.GetTempFileName();
        var uri = new Uri(tmp).AbsoluteUri; // file:///
        var ok = UrlSource.TryNormalize(uri, out var parsed, out var error);
        try
        {
            ok.Should().BeTrue();
            parsed!.Scheme.Should().Be(UrlSource.Kind.File);
            parsed!.LocalPath.Should().Be(Path.GetFullPath(tmp));
            parsed!.Normalized.Should().StartWith("file:");
            error.Should().BeNull();
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public void TryNormalize_rejects_unsupported_scheme()
    {
        var ok = UrlSource.TryNormalize("ftp://example.com/file.mp3", out var parsed, out var error);
        ok.Should().BeFalse();
        parsed.Should().BeNull();
        error.Should().Contain("Unsupported");
    }
}
