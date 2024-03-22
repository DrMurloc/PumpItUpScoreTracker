using System.Text.RegularExpressions;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct PreferenceRating : IComparable<PreferenceRating>
{
    private readonly decimal _level;

    private PreferenceRating(decimal levelInt)
    {
        _level = levelInt;
    }

    public static readonly PreferenceRating Max = new(5);
    public static readonly PreferenceRating Min = new(0);

    public override string ToString()
    {
        return _level.ToString();
    }


    public static implicit operator PreferenceRating(int levelInt)
    {
        return From(levelInt);
    }

    public static implicit operator int(PreferenceRating value)
    {
        return (int)value._level;
    }

    public static implicit operator PreferenceRating(decimal levelInt)
    {
        return From(levelInt);
    }

    public static implicit operator decimal(PreferenceRating value)
    {
        return value._level;
    }

    public static bool operator ==(PreferenceRating v1, PreferenceRating v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(PreferenceRating v1, PreferenceRating v2)
    {
        return !v1.Equals(v2);
    }

    public static bool operator >=(PreferenceRating v1, PreferenceRating v2)
    {
        return v1._level >= v2._level;
    }

    public static bool operator <=(PreferenceRating v1, PreferenceRating v2)
    {
        return v2 >= v1;
    }

    public static bool operator >(PreferenceRating v1, PreferenceRating v2)
    {
        return v1._level > v2._level;
    }

    public static bool operator <(PreferenceRating v1, PreferenceRating v2)
    {
        return v2 > v1;
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case PreferenceRating other:
                return Equals(other);
            default:
                return false;
        }
    }


    public static bool TryParse(string levelString, out PreferenceRating result)
    {
        result = default;
        return decimal.TryParse(levelString, out var levelInt) && TryParse(levelInt, out result);
    }

    public static bool TryParse(decimal levelInt, out PreferenceRating result)
    {
        try
        {
            result = From(levelInt);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }


    private bool Equals(PreferenceRating otherParam)
    {
        return _level == otherParam._level;
    }

    public override int GetHashCode()
    {
        return _level.GetHashCode();
    }

    public static PreferenceRating From(decimal level)
    {
        if (level < 0) throw new InvalidDifficultyLevelException("Rating must be greater than 0");

        if (level > 5) throw new InvalidDifficultyLevelException("Rating cannot be greater than 5");

        return new PreferenceRating(level);
    }

    private static readonly Regex _shortHandRegex = new(@"^\s*([A-Za-z]+)([0-9]+)\s*$", RegexOptions.Compiled);

    public int CompareTo(PreferenceRating other)
    {
        return _level.CompareTo(other._level);
    }
}