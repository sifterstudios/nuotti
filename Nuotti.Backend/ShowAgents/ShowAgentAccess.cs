using System.Text.Json.Serialization;

namespace Nuotti.Backend.ShowAgents;

[JsonConverter(typeof(JsonStringEnumConverter<ShowAgentConnectionState>))]
public enum ShowAgentConnectionState { Offline, Ready, Playing, Error }

public sealed record ShowAgentPairingCode(string Code, DateTimeOffset ExpiresAt);
public sealed record PairedShowAgent(string AgentId, string Credential, string AccessToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record ShowAgentLease(string AgentId, string WorkspaceId, string SessionCode, DateTimeOffset ExpiresAt);
public sealed record ShowAgentStatus(
    string AgentId, string Name, string WorkspaceId, string SessionCode,
    ShowAgentConnectionState State, string? Detail, DateTimeOffset? LastSeenAt, bool Revoked);
public sealed record ShowAgentCommand(long Sequence, string MessageType, object Payload);
public sealed class ShowAgentPairingThrottledException : System.Exception;

public interface IShowAgentAccessStore
{
    Task<ShowAgentPairingCode> IssuePairingCodeAsync(string workspaceId, string sessionCode, string issuedBy,
        CancellationToken cancellationToken = default);
    Task<PairedShowAgent?> PairAsync(string code, string name, CancellationToken cancellationToken = default);
    Task<(string Token, ShowAgentLease Lease)?> IssueAccessTokenAsync(string credential,
        CancellationToken cancellationToken = default);
    Task<ShowAgentLease?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<bool> ReportStatusAsync(ShowAgentLease lease, ShowAgentConnectionState state, string? detail,
        CancellationToken cancellationToken = default);
    Task<ShowAgentStatus?> GetStatusAsync(string workspaceId, string sessionCode,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string workspaceId, string sessionCode, CancellationToken cancellationToken = default);
    Task AppendCommandAsync(string workspaceId, string sessionCode, string messageType, object payload,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShowAgentCommand>?> ReadCommandsAsync(ShowAgentLease lease, long afterSequence,
        CancellationToken cancellationToken = default);
}

internal static class ShowAgentTokens
{
    internal static string NewSecret() => Convert.ToBase64String(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    internal static string Hash(string value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    internal static string PairingCode() => System.Security.Cryptography.RandomNumberGenerator
        .GetInt32(0, 100_000_000).ToString("D8");
}
