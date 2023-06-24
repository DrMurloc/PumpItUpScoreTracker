using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct XXScore
{
    private readonly int _score;

    private XXScore(int score)
    {
        _score = score;
    }

    public static XXScore Min = new(0);
    public static XXScore Max = new(200000000);

    public override string ToString()
    {
        return _score.ToString();
    }

    public static implicit operator XXScore(int scoreInt)
    {
        return From(scoreInt);
    }

    public static implicit operator int(XXScore value)
    {
        return value._score;
    }

    public static bool operator ==(XXScore v1, XXScore v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(XXScore v1, XXScore v2)
    {
        return !v1.Equals(v2);
    }

    public static bool operator >(XXScore v1, XXScore v2)
    {
        return v1._score > v2._score;
    }

    public static bool operator <(XXScore v1, XXScore v2)
    {
        return v2 > v1;
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case XXScore other:
                return Equals(other);
            default:
                return false;
        }
    }


    public static bool TryParse(string scoreString, out XXScore? result)
    {
        if (string.IsNullOrWhiteSpace(scoreString))
        {
            result = null;
            return true;
        }

        var couldParse = TryParse(scoreString, out XXScore value);
        result = value;
        return couldParse;
    }

    public static bool TryParse(string scoreString, out XXScore result)
    {
        result = default;
        return int.TryParse(scoreString, out var scoreInt) && TryParse(scoreInt, out result);
    }

    public static bool TryParse(int scoreInt, out XXScore result)
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


    private bool Equals(XXScore otherParam)
    {
        return _score == otherParam._score;
    }

    public override int GetHashCode()
    {
        return _score.GetHashCode();
    }

    public static XXScore From(int score)
    {
        if (score < Min._score) throw new InvalidScoreException("Level must be greater than 0");

        if (score > Max._score) throw new InvalidScoreException("Level cannot be greater than 28");

        return new XXScore(score);
    }
}