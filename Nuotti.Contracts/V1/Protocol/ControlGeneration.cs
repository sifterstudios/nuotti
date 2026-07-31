using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>A monotonic fencing token identifying the only current Session controller.</summary>
[JsonConverter(typeof(ControlGenerationJsonConverter))]
public readonly record struct ControlGeneration
{
    public static ControlGeneration Initial { get; } = new(0);

    public long Value { get; }

    public ControlGeneration(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public ControlGeneration Next() => new(checked(Value + 1));

    public static implicit operator long(ControlGeneration value) => value.Value;
    public static explicit operator ControlGeneration(long value) => new(value);

    private sealed class ControlGenerationJsonConverter : JsonConverter<ControlGeneration>
    {
        public override ControlGeneration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(long.Parse(reader.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture));

        public override void Write(Utf8JsonWriter writer, ControlGeneration value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
