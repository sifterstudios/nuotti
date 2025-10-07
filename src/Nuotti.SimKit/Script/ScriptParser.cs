using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Nuotti.SimKit.Script;

public static class ScriptParser
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ScriptModel ParseJson(string json)
        => JsonSerializer.Deserialize<ScriptModel>(json, JsonOptions)
           ?? throw new InvalidOperationException("Invalid JSON script");

    public static ScriptModel ParseYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var model = deserializer.Deserialize<ScriptModel>(yaml);
        return model ?? throw new InvalidOperationException("Invalid YAML script");
    }
}