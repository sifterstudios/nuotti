using Json.Schema;
using Nuotti.Contracts.V1;
using Nuotti.Contracts.V1.Model;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
namespace Nuotti.SimKit.Script;

public static class ScenarioParser
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Use Contracts JSON policy (camelCase) for consistent schema validation
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = ContractsJson.RestOptions.PropertyNamingPolicy,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = ContractsJson.RestOptions.WriteIndented,
        DictionaryKeyPolicy = ContractsJson.RestOptions.DictionaryKeyPolicy
    };

    // Basic JSON Schema for scenario files
    // Note: kept intentionally simple per acceptance criteria
    private const string ScenarioSchemaText = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "$id": "https://nuotti.dev/schemas/scenario.json",
      "type": "object",
      "required": ["sessions", "songs"],
      "properties": {
        "audience": {
          "type": "object",
          "properties": {
            "demographic": { "type": "string" },
            "expectedSize": { "type": "integer", "minimum": 0 },
            "energy": { "type": "string", "enum": ["Low", "Medium", "High"] }
          },
          "additionalProperties": false
        },
        "sessions": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "object",
            "required": ["id", "playlist"],
            "properties": {
              "id": { "type": "string", "minLength": 1 },
              "name": { "type": "string" },
              "playlist": {
                "type": "array",
                "minItems": 1,
                "items": {
                  "type": "object",
                  "required": ["songId"],
                  "properties": {
                    "songId": { "type": "string", "minLength": 1 }
                  },
                  "additionalProperties": false
                }
              }
            },
            "additionalProperties": false
          }
        },
        "songs": {
          "type": "array",
          "minItems": 1,
          "items": {
            "type": "object",
            "required": ["id", "title", "artist", "phases"],
            "properties": {
              "id": { "type": "string", "minLength": 1 },
              "title": { "type": "string", "minLength": 1 },
              "artist": { "type": "string", "minLength": 1 },
              "phases": {
                "type": "array",
                "minItems": 1,
                "items": {
                  "type": "object",
                  "required": ["name", "durationMs"],
                  "properties": {
                    "name": { "type": "string", "minLength": 1 },
                    "durationMs": { "type": "integer", "minimum": 0 }
                  },
                  "additionalProperties": false
                }
              }
            },
            "additionalProperties": false
          }
        }
      },
      "additionalProperties": false
    }
    """;

    private static readonly JsonSchema ScenarioSchema = JsonSchema.FromText(ScenarioSchemaText);

    public static ScenarioModel ParseJson(string json)
    {
        ValidateJsonAgainstSchema(json, out var node);
        // At this point schema is satisfied; deserialize strongly typed
        return JsonSerializer.Deserialize<ScenarioModel>(json, JsonOptions)
               ?? throw new InvalidOperationException("Invalid scenario JSON: deserialization returned null");
    }

    public static ScenarioModel ParseYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        // Deserialize to typed model first (ensures numeric types are retained correctly)
        var typed = deserializer.Deserialize<ScenarioModel>(yaml)
                    ?? throw new InvalidOperationException("Invalid scenario YAML: deserialization returned null");
        // Validate by serializing the typed model to JSON
        var json = JsonSerializer.Serialize(typed, JsonOptions);
        ValidateJsonAgainstSchema(json, out _);
        return typed;
    }

    private static void ValidateJsonAgainstSchema(string json, out JsonNode node)
    {
        try
        {
            node = JsonNode.Parse(json)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid scenario JSON: cannot parse. {ex.Message}");
        }

        var results = ScenarioSchema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!results.IsValid)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scenario validation failed:");
            foreach (var detail in results.Details)
            {
                if (detail.HasErrors)
                {
                    foreach (var kv in detail.Errors!)
                    {
                        var instancePath = detail.InstanceLocation.ToString();
                        if (string.IsNullOrEmpty(instancePath)) instancePath = "$";
                        sb.Append(" - ").Append(instancePath).Append(": ").AppendLine(kv.Value);
                    }
                }
            }
            var message = sb.ToString().TrimEnd();
            throw new InvalidOperationException(message);
        }
    }
}
