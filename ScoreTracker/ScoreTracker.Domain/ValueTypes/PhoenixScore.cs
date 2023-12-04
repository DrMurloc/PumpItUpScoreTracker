using System.Text.Json;
using System.Text.Json.Serialization;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct PhoenixScore
{
    private readonly int _score;

    private PhoenixScore(int score)
    {
        _score = score;
    }

    public static PhoenixScore Min = new(0);
    public static PhoenixScore Max = new(1000000);

    public PhoenixScore Round(int zeros = 4)
    {
        return (int)(Math.Round(_score / Math.Pow(10, zeros)) * Math.Pow(10, zeros));
    }

    public override string ToString()
    {
        return _score.ToString();
    }

    public static implicit operator PhoenixScore(int scoreInt)
    {
        return From(scoreInt);
    }

    public static implicit operator int(PhoenixScore value)
    {
        return value._score;
    }

    public static bool operator ==(PhoenixScore v1, PhoenixScore v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(PhoenixScore v1, PhoenixScore v2)
    {
        return !v1.Equals(v2);
    }

    public static bool operator >(PhoenixScore v1, PhoenixScore v2)
    {
        return v1._score > v2._score;
    }

    public static bool operator <(PhoenixScore v1, PhoenixScore v2)
    {
        return v2 > v1;
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case PhoenixScore other:
                return Equals(other);
            default:
                return false;
        }
    }


    public static bool TryParse(string scoreString, out PhoenixScore? result)
    {
        if (string.IsNullOrWhiteSpace(scoreString))
        {
            result = null;
            return true;
        }

        var couldParse = TryParse(scoreString, out PhoenixScore value);
        result = value;
        return couldParse;
    }

    public static bool TryParse(string scoreString, out PhoenixScore result)
    {
        result = default;
        return int.TryParse(scoreString, out var scoreInt) && TryParse(scoreInt, out result);
    }

    public static bool IsValid(int scoreInt)
    {
        return TryParse(scoreInt, out _);
    }

    public static bool TryParse(int scoreInt, out PhoenixScore result)
    {
        try
        {
            result = From(scoreInt);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    public PhoenixLetterGrade LetterGrade
    {
        get
        {
            var score = _score;
            return Enum.GetValues<PhoenixLetterGrade>().First(letter =>
                letter.GetMinimumScore() <= score && score <= letter.GetMaximumScore());
        }
    }

    private bool Equals(PhoenixScore otherParam)
    {
        return _score == otherParam._score;
    }

    public override int GetHashCode()
    {
        return _score.GetHashCode();
    }

    public static PhoenixScore From(int score)
    {
        if (score < Min._score) throw new InvalidScoreException("Level must be greater than 0");

        if (score > Max._score) throw new InvalidScoreException("Level cannot be greater than 28");

        return new PhoenixScore(score);
    }

    public static JsonConverter Converter = new PhoenixScoreConverter();

    private class PhoenixScoreConverter : JsonConverter<PhoenixScore>
    {
        public override PhoenixScore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return From(reader.GetInt32());
        }

        public override void Write(Utf8JsonWriter writer, PhoenixScore value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value._score);
        }
    }
}