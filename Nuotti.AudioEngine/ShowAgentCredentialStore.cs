using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Nuotti.AudioEngine;

public interface IShowAgentCredentialStore
{
    string? Load();
    void Save(string credential);
    long LoadCursor(string workspaceId, string sessionCode);
    void SaveCursor(string workspaceId, string sessionCode, long sequence);
    void Delete();
}

public interface ICredentialProtector
{
    byte[] Protect(byte[] value);
    byte[] Unprotect(byte[] value);
}

/// <summary>Windows CurrentUser DPAPI; copied files cannot be decrypted by another user or machine.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiCredentialProtector : ICredentialProtector
{
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Nuotti.ShowAgent.Credential.v1");
    public byte[] Protect(byte[] value) => ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] value) => ProtectedData.Unprotect(value, Entropy, DataProtectionScope.CurrentUser);
}

public sealed class FileShowAgentCredentialStore(string path, ICredentialProtector protector) : IShowAgentCredentialStore
{
    sealed record LocalState(string Credential, string? WorkspaceId = null, string? SessionCode = null, long Cursor = 0);

    public string? Load()
    {
        return Read()?.Credential;
    }

    public void Save(string credential)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        Write(new LocalState(credential));
    }

    public long LoadCursor(string workspaceId, string sessionCode)
    {
        var state = Read();
        return state?.WorkspaceId == workspaceId && state.SessionCode == sessionCode ? state.Cursor : 0;
    }

    public void SaveCursor(string workspaceId, string sessionCode, long sequence)
    {
        var state = Read() ?? throw new InvalidOperationException("Show Agent credential is missing.");
        Write(state with { WorkspaceId = workspaceId, SessionCode = sessionCode, Cursor = sequence });
    }

    public void Delete()
    {
        if (File.Exists(path)) File.Delete(path);
    }

    LocalState? Read()
    {
        if (!File.Exists(path)) return null;
        var clear = Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(path)));
        try { return JsonSerializer.Deserialize<LocalState>(clear); }
        catch (JsonException) { return new LocalState(clear); }
    }

    void Write(LocalState state)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        var clear = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state));
        File.WriteAllBytes(temporary, protector.Protect(clear));
        File.Move(temporary, path, overwrite: true);
    }

    public static FileShowAgentCredentialStore CreateDefault()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Show Agent credentials require Windows DPAPI.");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new(Path.Combine(root, "Nuotti", "ShowAgent", "credential.bin"), new WindowsDpapiCredentialProtector());
    }
}
