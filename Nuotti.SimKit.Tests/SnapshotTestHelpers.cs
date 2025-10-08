using Nuotti.Contracts.V1.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Sdk;
namespace Nuotti.SimKit.Tests;

internal static class SnapshotTestHelpers
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    static SnapshotTestHelpers()
    {
        WriteOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static string SerializeSnapshots(IEnumerable<GameStateSnapshot> snapshots)
    {
        return JsonSerializer.Serialize(snapshots, WriteOptions);
    }

    public static void AssertJsonSequenceFuzzyEqual(string expectedJson, string actualJson)
    {
        using var expectedDoc = JsonDocument.Parse(expectedJson);
        using var actualDoc = JsonDocument.Parse(actualJson);
        var expected = expectedDoc.RootElement;
        var actual = actualDoc.RootElement;
        if (expected.ValueKind != JsonValueKind.Array || actual.ValueKind != JsonValueKind.Array)
            throw new XunitException("Both JSON roots must be arrays of snapshots");

        if (expected.GetArrayLength() != actual.GetArrayLength())
            throw new XunitException($"Snapshot count mismatch. Expected {expected.GetArrayLength()}, Actual {actual.GetArrayLength()}");

        for (int i = 0; i < expected.GetArrayLength(); i++)
        {
            if (!AreEqualFuzzy(expected[i], actual[i]))
            {
                throw new XunitException($"Snapshot at index {i} differs.\nExpected: {expected[i]}\nActual:   {actual[i]}");
            }
        }
    }

    private static bool AreEqualFuzzy(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    // Compare by property names, excluding volatile ones
                    var aProps = a.EnumerateObject().Where(p => !IsVolatile(p.Name)).OrderBy(p => p.Name).ToList();
                    var bProps = b.EnumerateObject().Where(p => !IsVolatile(p.Name)).OrderBy(p => p.Name).ToList();
                    if (aProps.Count != bProps.Count) return false;
                    for (int i = 0; i < aProps.Count; i++)
                    {
                        if (aProps[i].Name != bProps[i].Name) return false;
                        if (!AreEqualFuzzy(aProps[i].Value, bProps[i].Value)) return false;
                    }
                    return true;
                }
            case JsonValueKind.Array:
                {
                    var aArr = a.EnumerateArray().ToList();
                    var bArr = b.EnumerateArray().ToList();
                    if (aArr.Count != bArr.Count) return false;
                    for (int i = 0; i < aArr.Count; i++)
                    {
                        if (!AreEqualFuzzy(aArr[i], bArr[i])) return false;
                    }
                    return true;
                }
            default:
                return a.ToString() == b.ToString();
        }
    }

    private static bool IsVolatile(string name)
    {
        // Tolerate non-deterministic stamps/ids
        if (string.Equals(name, "songStartedAtUtc", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(name, "timestamp", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
