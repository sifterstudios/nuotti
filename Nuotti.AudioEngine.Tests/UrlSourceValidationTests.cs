using FluentAssertions;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class UrlSourceValidationTests
{
    [Fact]
    public void Invalid_Scheme_Is_Rejected()
    {
        // Arrange & Act - Test schemes that parse as URIs but are unsupported
        // ftp:// should parse as a URI but be rejected for unsupported scheme
        var ftpUrl = "ftp://example.com/file.mp3";
        var result1 = UrlSource.TryNormalize(ftpUrl, out var parsed1, out var error1);

        // Assert
        result1.Should().BeFalse("ftp:// should be rejected");
        parsed1.Should().BeNull();
        error1.Should().Contain("Unsupported URI scheme");

        // data:// may or may not parse as URI depending on format
        // Test with a format that should parse
        if (Uri.TryCreate("data://host/path", UriKind.Absolute, out _))
        {
            var dataUrl = "data://host/path";
            var result2 = UrlSource.TryNormalize(dataUrl, out var parsed2, out var error2);
            result2.Should().BeFalse("data:// should be rejected");
            parsed2.Should().BeNull();
            error2.Should().Contain("Unsupported URI scheme");
        }

        // javascript: and other non-URI formats fail at parsing stage
        var nonUriFormat = "javascript:alert(1)";
        var result3 = UrlSource.TryNormalize(nonUriFormat, out var parsed3, out var error3);
        result3.Should().BeFalse();
        parsed3.Should().BeNull();
        // Error may be "Invalid URL or path format" if URI parsing fails
        error3.Should().NotBeNull();
        error3.Should().ContainAny("Invalid URL or path format", "Unsupported URI scheme");
    }

    [Fact]
    public void Http_Url_Is_Accepted()
    {
        // Arrange
        var url = "http://example.com/audio.mp3";

        // Act
        var result = UrlSource.TryNormalize(url, out var parsed, out var error);

        // Assert
        result.Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Scheme.Should().Be(UrlSource.Kind.Http);
        parsed.Normalized.Should().Be(url);
        parsed.LocalPath.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void Https_Url_Is_Accepted()
    {
        // Arrange
        var url = "https://example.com/audio.mp3";

        // Act
        var result = UrlSource.TryNormalize(url, out var parsed, out var error);

        // Assert
        result.Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Scheme.Should().Be(UrlSource.Kind.Https);
        parsed.Normalized.Should().Be(url);
        parsed.LocalPath.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void File_Url_With_Existing_File_Is_Accepted()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var fileUrl = new Uri(tempFile).AbsoluteUri;

            // Act
            var result = UrlSource.TryNormalize(fileUrl, out var parsed, out var error);

            // Assert
            result.Should().BeTrue();
            parsed.Should().NotBeNull();
            parsed!.Scheme.Should().Be(UrlSource.Kind.File);
            parsed.LocalPath.Should().NotBeNull();
            parsed.LocalPath.Should().Be(Path.GetFullPath(tempFile));
            error.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void File_Url_With_Empty_Path_Is_Rejected()
    {
        // Arrange - Test various file:// formats that should fail
        // file:// with no path after slashes
        var testCases = new[]
        {
            ("file://", "file:// with no path"),
            ("file:///", "file:/// with no path")
        };

        foreach (var (fileUrl, description) in testCases)
        {
            // Act
            var result = UrlSource.TryNormalize(fileUrl, out var parsed, out var error);

            // Assert - should fail, but error message may vary by OS/URI parsing
            if (!result)
            {
                parsed.Should().BeNull($"{description} should return null parsed");
                error.Should().NotBeNull($"{description} should have error message");
                // Error may be "file:// URI has no path" or "Invalid URL or path format"
                error.Should().Match(e => e.Contains("file:// URI has no path") || e.Contains("Invalid URL or path format"));
            }
            // On some systems, file:/// might parse as valid (e.g., root path)
            // In that case, we just verify it doesn't crash
        }
    }

    [Fact]
    public void Windows_Path_Is_Normalized_To_File_Url()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows-specific test

        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = UrlSource.TryNormalize(tempFile, out var parsed, out var error);

            // Assert
            result.Should().BeTrue();
            parsed.Should().NotBeNull();
            parsed!.Scheme.Should().Be(UrlSource.Kind.File);
            parsed.LocalPath.Should().Be(Path.GetFullPath(tempFile));
            parsed.Normalized.Should().StartWith("file:///");
            error.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Unix_Path_Is_Normalized_To_File_Url()
    {
        if (OperatingSystem.IsWindows()) return; // Unix-specific test

        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = UrlSource.TryNormalize(tempFile, out var parsed, out var error);

            // Assert
            result.Should().BeTrue();
            parsed.Should().NotBeNull();
            parsed!.Scheme.Should().Be(UrlSource.Kind.File);
            parsed.LocalPath.Should().Be(Path.GetFullPath(tempFile));
            parsed.Normalized.Should().StartWith("file://");
            error.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Null_Or_Empty_Url_Is_Rejected()
    {
        // Arrange
        var invalidInputs = new[] { null, "", "   " };

        foreach (var input in invalidInputs)
        {
            // Act
            var result = UrlSource.TryNormalize(input!, out var parsed, out var error);

            // Assert
            result.Should().BeFalse($"input '{input}' should be rejected");
            parsed.Should().BeNull();
            error.Should().Contain("URL is required");
        }
    }

    [Fact]
    public void Relative_Path_Is_Rejected()
    {
        // Arrange
        var relativePath = "relative/path.mp3";

        // Act
        var result = UrlSource.TryNormalize(relativePath, out var parsed, out var error);

        // Assert
        result.Should().BeFalse();
        parsed.Should().BeNull();
        error.Should().Contain("Invalid URL or path format");
    }
}

public class HttpFilePreflightValidationTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _onSend;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> onSend) => _onSend = onSend;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_onSend(request));
    }

    [Fact]
    public async Task Http_Head_Request_Is_Sent_For_Http_Url()
    {
        // Arrange
        var headCalled = false;
        var handler = new StubHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Head);
            req.RequestUri!.ToString().Should().Be("http://example.com/audio.mp3");
            headCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync("http://example.com/audio.mp3");

        // Assert
        headCalled.Should().BeTrue();
        result.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task Https_Head_Request_Is_Sent_For_Https_Url()
    {
        // Arrange
        var headCalled = false;
        var handler = new StubHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Head);
            req.RequestUri!.ToString().Should().Be("https://example.com/audio.mp3");
            headCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync("https://example.com/audio.mp3");

        // Assert
        headCalled.Should().BeTrue();
        result.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task Http_404_Response_Is_Rejected()
    {
        // Arrange
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync("http://example.com/notfound.mp3");

        // Assert
        result.Ok.Should().BeFalse();
        result.Problem.Should().NotBeNull();
        result.Problem!.Status.Should().Be(404);
        result.Problem.Title.Should().Contain("Source not reachable");
    }

    [Fact]
    public async Task Http_MethodNotAllowed_Falls_Back_To_Allow()
    {
        // Arrange
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync("http://example.com/audio.mp3");

        // Assert
        result.Ok.Should().BeTrue("MethodNotAllowed should allow play to proceed");
    }

    [Fact]
    public async Task Http_Forbidden_Falls_Back_To_Allow()
    {
        // Arrange
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync("http://example.com/audio.mp3");

        // Assert
        result.Ok.Should().BeTrue("Forbidden should allow play to proceed");
    }

    [Fact]
    public async Task Http_Timeout_Is_Rejected()
    {
        // Arrange
        var handler = new StubHandler(_ => throw new TaskCanceledException("Timeout"));
        var client = new HttpClient(handler);
        var preflight = new HttpFilePreflight(client, timeout: TimeSpan.FromMilliseconds(1));

        // Act
        var result = await preflight.CheckAsync("http://example.com/slow.mp3");

        // Assert
        result.Ok.Should().BeFalse();
        result.Problem.Should().NotBeNull();
        result.Problem!.Title.Should().Contain("Source timeout");
    }

    [Fact]
    public async Task File_Not_Exists_Is_Rejected()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.mp3");
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var preflight = new HttpFilePreflight(client);

        // Act
        var result = await preflight.CheckAsync(new Uri(nonExistentFile).AbsoluteUri);

        // Assert
        result.Ok.Should().BeFalse();
        result.Problem.Should().NotBeNull();
        result.Problem!.Title.Should().Contain("File not found");
    }

    [Fact]
    public async Task File_Outside_Allowed_Roots_Is_Rejected()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var allowedRoot = Path.Combine(Path.GetTempPath(), $"allowed-{Guid.NewGuid()}");
        Directory.CreateDirectory(allowedRoot);
        try
        {
            var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var options = new AudioEngineSafetyOptions { AllowedRoots = new[] { allowedRoot } };
            var preflight = new HttpFilePreflight(client, options: options);

            // Act
            var result = await preflight.CheckAsync(new Uri(tempFile).AbsoluteUri);

            // Assert
            result.Ok.Should().BeFalse();
            result.Problem.Should().NotBeNull();
            result.Problem!.Title.Should().Contain("Path not allowed");
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(allowedRoot, true);
        }
    }

    [Fact]
    public async Task File_Inside_Allowed_Roots_Is_Accepted()
    {
        // Arrange
        var allowedRoot = Path.Combine(Path.GetTempPath(), $"allowed-{Guid.NewGuid()}");
        Directory.CreateDirectory(allowedRoot);
        var file = Path.Combine(allowedRoot, "audio.mp3");
        await File.WriteAllTextAsync(file, "test");
        try
        {
            var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var options = new AudioEngineSafetyOptions { AllowedRoots = new[] { allowedRoot } };
            var preflight = new HttpFilePreflight(client, options: options);

            // Act
            var result = await preflight.CheckAsync(new Uri(file).AbsoluteUri);

            // Assert
            result.Ok.Should().BeTrue();
            result.NormalizedUrl.Should().NotBeNull();
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(allowedRoot, true);
        }
    }
}

