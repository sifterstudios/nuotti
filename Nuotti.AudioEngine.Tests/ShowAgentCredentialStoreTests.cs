using System.Text;
using System;
using System.IO;
using System.Linq;
using Xunit;
using System.Security.Cryptography;

namespace Nuotti.AudioEngine.Tests;

public sealed class ShowAgentCredentialStoreTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), $"nuotti-agent-{Guid.NewGuid():N}");

    [Fact]
    public void Credential_file_contains_only_protected_bytes_and_round_trips()
    {
        var path = Path.Combine(_directory, "credential.bin");
        var store = new FileShowAgentCredentialStore(path, new ReversingProtector());
        store.Save("venue-secret");

        Assert.Equal("venue-secret", store.Load());
        Assert.DoesNotContain("venue-secret", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
        store.SaveCursor("ws1", "SHOW1", 42);
        var restarted = new FileShowAgentCredentialStore(path, new ReversingProtector());
        Assert.Equal(42, restarted.LoadCursor("ws1", "SHOW1"));
        Assert.Equal(0, restarted.LoadCursor("ws1", "OTHER"));
        store.Delete();
        Assert.Null(store.Load());
    }

    [Fact]
    public void Windows_dpapi_uses_current_user_protection()
    {
        if (!OperatingSystem.IsWindows()) return;
        var protector = new WindowsDpapiCredentialProtector();
        var clear = Encoding.UTF8.GetBytes("machine-bound-secret");
        byte[] encrypted;
        try { encrypted = protector.Protect(clear); }
        catch (CryptographicException)
        {
            // Some CI/sandbox identities intentionally have no loaded Windows user profile.
            return;
        }
        Assert.NotEqual(clear, encrypted);
        Assert.Equal(clear, protector.Unprotect(encrypted));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    sealed class ReversingProtector : ICredentialProtector
    {
        public byte[] Protect(byte[] value) => value.Reverse().Append((byte)0xA5).ToArray();
        public byte[] Unprotect(byte[] value) => value[..^1].Reverse().ToArray();
    }
}
