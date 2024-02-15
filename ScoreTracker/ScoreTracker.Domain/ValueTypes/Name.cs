using System.Text.Json;
using System.Text.Json.Serialization;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct Name
{
    private readonly string _name;

    private Name(string nameString)
    {
        _name = nameString;
    }

    public override string ToString()
    {
        return _name;
    }

    public static implicit operator Name(string nameString)
    {
        return From(nameString);
    }

    public static implicit operator Name?(string? nameString)
    {
        if (nameString == null) return null;

        return From(nameString);
    }

    public static implicit operator string(Name value)
    {
        return value._name;
    }

    public static bool operator ==(Name v1, Name v2)
    {
        return v1.equals(v2);
    }

    public static bool operator !=(Name v1, Name v2)
    {
        return !v1.equals(v2);
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case Name other:
                return equals(other);
            default:
                return false;
        }
    }

    public static bool TryParse(string nameString, out Name result)
    {
        try
        {
            result = From(nameString);
            return true;
        }
        catch
        {
            result = new Name("Invalid");
            return false;
        }
    }


    private bool equals(Name otherParam)
    {
        return string.Equals(_name, otherParam._name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return _name.ToLower().GetHashCode();
    }

    public static Name From(string nameParameterParam)
    {
        if (nameParameterParam == null) throw new InvalidNameException("Name was null.");

        if (string.IsNullOrWhiteSpace(nameParameterParam)) throw new InvalidNameException("Name was empty.");

        return new Name(nameParameterParam.Trim());
    }

    public static readonly JsonConverter Converter = new NameConverter();

    private class NameConverter : JsonConverter<Name>
    {
        public override Name Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return From(reader.GetString() ?? "");
        }

        public override void Write(Utf8JsonWriter writer, Name value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value._name);
        }
    }
}