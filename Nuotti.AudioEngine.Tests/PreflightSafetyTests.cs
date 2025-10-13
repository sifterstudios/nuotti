using FluentAssertions;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Nuotti.AudioEngine.Tests;

public class PreflightSafetyTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _onSend;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> onSend) => _onSend = onSend;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_onSend(request));
    }

    [Fact]
    public async Task File_path_outside_allowed_roots_is_rejected()
    {
        // Arrange
        var tmpFile = Path.GetTempFileName();
        var otherDir = Path.Combine(Path.GetTempPath(), "nuotti-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(otherDir);
        var allowed = otherDir; // different from file's directory
        try
        {
            var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var opts = new AudioEngineSafetyOptions { AllowedRoots = new[] { allowed } };
            var pf = new HttpFilePreflight(client, TimeSpan.FromSeconds(1), opts);

            // Act
            var res = await pf.CheckAsync(new Uri(tmpFile).AbsoluteUri);

            // Assert
            res.Ok.Should().BeFalse();
            res.Problem.Should().NotBeNull();
            res.Problem!.Title.Should().Contain("Path not allowed");
        }
        finally
        {
            try { File.Delete(tmpFile); } catch { }
            try { Directory.Delete(otherDir, true); } catch { }
        }
    }

    [Fact]
    public async Task File_path_inside_allowed_roots_is_accepted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nuotti-allowed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "a.tmp");
        await File.WriteAllTextAsync(file, "x");
        try
        {
            var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var opts = new AudioEngineSafetyOptions { AllowedRoots = new[] { dir } };
            var pf = new HttpFilePreflight(client, TimeSpan.FromSeconds(1), opts);

            var res = await pf.CheckAsync(new Uri(file).AbsoluteUri);
            res.Ok.Should().BeTrue("file under allowed root should pass");
        }
        finally
        {
            try { File.Delete(file); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task Http_oversize_is_rejected_when_known()
    {
        // Arrange
        var handler = new StubHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Head);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(Array.Empty<byte>());
            resp.Content.Headers.ContentLength = 10 * 1024 * 1024; // 10 MB
            return resp;
        });
        var client = new HttpClient(handler);
        var opts = new AudioEngineSafetyOptions { MaxHttpSizeMB = 5 };
        var pf = new HttpFilePreflight(client, TimeSpan.FromSeconds(1), opts);

        // Act
        var res = await pf.CheckAsync("https://example.com/big.mp3");

        // Assert
        res.Ok.Should().BeFalse();
        res.Problem!.Detail.Should().Contain("exceeds limit");
    }

    [Fact]
    public async Task Http_unknown_size_is_allowed_when_limit_set()
    {
        var handler = new StubHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Head);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(Array.Empty<byte>());
            // No Content-Length
            return resp;
        });
        var client = new HttpClient(handler);
        var opts = new AudioEngineSafetyOptions { MaxHttpSizeMB = 5 };
        var pf = new HttpFilePreflight(client, TimeSpan.FromSeconds(1), opts);

        var res = await pf.CheckAsync("https://example.com/unknown.mp3");
        res.Ok.Should().BeTrue();
    }
}
