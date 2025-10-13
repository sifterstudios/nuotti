using System.Text.Json;
namespace Nuotti.AudioEngine.AudioDevices;

public static class DeviceListJson
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(DeviceListResult result)
        => JsonSerializer.Serialize(result, Options);

    public static DeviceListResult Deserialize(string json)
        => JsonSerializer.Deserialize<DeviceListResult>(json, Options)!
            ?? throw new InvalidOperationException("Invalid device list JSON");
}
