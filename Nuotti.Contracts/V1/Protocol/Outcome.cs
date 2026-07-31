using System.Text.Json.Serialization;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>The durable, explicit disposition of one Command intent.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Outcome>))]
public enum Outcome
{
    Applied,
    Duplicate,
    Rejected
}
