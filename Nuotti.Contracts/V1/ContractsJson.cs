using System.Text.Json;

namespace Nuotti.Contracts.V1;

/// <summary>
/// Centralized JSON options for Nuotti contract serialization.
/// - REST APIs use camelCase.
/// - SignalR hub messages use PascalCase (as declared on DTOs).
/// </summary>
public static class ContractsJson
{
    /// <summary>
    /// Default JSON options for Nuotti.Contracts consumers.
    /// Currently defaults to REST (camelCase) to preserve existing behavior.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = CreateRestOptions();

    /// <summary>
    /// JSON options for REST endpoints: camelCase for property names and dictionary keys.
    /// </summary>
    public static readonly JsonSerializerOptions RestOptions = CreateRestOptions();

    /// <summary>
    /// JSON options for SignalR hub payloads: PascalCase (no naming policy) to match DTO property names.
    /// </summary>
    public static readonly JsonSerializerOptions HubOptions = CreateHubOptions();

    private static JsonSerializerOptions CreateRestOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    private static JsonSerializerOptions CreateHubOptions()
    {
        return new JsonSerializerOptions
        {
            // Null policy means "as-declared" on DTOs, which are PascalCase in C#.
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            WriteIndented = false
        };
    }
}
