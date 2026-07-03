using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoreTracker.SharedKernel.ValueTypes;

/// <summary>
///     A secret that must never appear in logs. ToString — which record ToString, string
///     interpolation, and structured-logging property capture all funnel through — yields only
///     a mask; the raw value crosses out through the implicit string conversion (or Reveal())
///     at the boundary that consumes it. The JSON converter round-trips the real value so
///     serialized messages still carry it.
/// </summary>
[JsonConverter(typeof(RedactedStringConverter))]
public readonly struct RedactedString
{
    private readonly string _value;

    private RedactedString(string value)
    {
        _value = value;
    }

    public string Reveal()
    {
        return _value ?? string.Empty;
    }

    public override string ToString()
    {
        return "***";
    }

    public static RedactedString From(string value)
    {
        return new RedactedString(value ?? string.Empty);
    }

    public static implicit operator RedactedString(string value)
    {
        return From(value);
    }

    public static implicit operator string(RedactedString value)
    {
        return value.Reveal();
    }

    public sealed class RedactedStringConverter : JsonConverter<RedactedString>
    {
        public override RedactedString Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            return From(reader.GetString() ?? "");
        }

        public override void Write(Utf8JsonWriter writer, RedactedString value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Reveal());
        }
    }
}
