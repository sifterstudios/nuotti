using System.Text.Json;
using Nuotti.Contracts.V1;

namespace Nuotti.UnitTests.TestHelpers;

/// <summary>
/// Helper methods for loading and working with JSON fixtures.
/// </summary>
public static class FixtureHelpers
{
    /// <summary>
    /// Loads a JSON fixture file from the Fixtures directory and deserializes it.
    /// </summary>
    public static T LoadFixture<T>(string fileName, JsonSerializerOptions? options = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var filePath = Path.Combine(basePath, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Fixture file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, options ?? ContractsJson.RestOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize fixture: {fileName}");
    }

    /// <summary>
    /// Saves an object as a JSON fixture file.
    /// </summary>
    public static void SaveFixture<T>(T value, string fileName, JsonSerializerOptions? options = null)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Directory.CreateDirectory(basePath);
        var filePath = Path.Combine(basePath, fileName);

        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(options ?? ContractsJson.RestOptions)
        {
            WriteIndented = true
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Gets the path to a fixture file.
    /// </summary>
    public static string GetFixturePath(string fileName)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        return Path.Combine(basePath, fileName);
    }
}

