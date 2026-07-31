using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Nuotti.Contracts.V1.Protocol;

/// <summary>A monotonically increasing position in one Session's durable Event stream.</summary>
[JsonConverter(typeof(SessionSequenceJsonConverter))]
public readonly record struct SessionSequence
{
    public static SessionSequence None { get; } = new(0);

    public long Value { get; }

    public SessionSequence(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public SessionSequence Next() => new(checked(Value + 1));

    public static implicit operator long(SessionSequence value) => value.Value;
    public static explicit operator SessionSequence(long value) => new(value);

    private sealed class SessionSequenceJsonConverter : JsonConverter<SessionSequence>
    {
        public override SessionSequence Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(long.Parse(reader.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture));

        public override void Write(Utf8JsonWriter writer, SessionSequence value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
