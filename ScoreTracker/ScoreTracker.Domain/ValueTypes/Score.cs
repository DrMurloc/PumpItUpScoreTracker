using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct Score
{
    private readonly int _score;

    private Score(int score)
    {
        _score = score;
    }

    public static Score Min = new(0);
    public static Score Max = new(200000000);

    public override string ToString()
    {
        return _score.ToString();
    }

    public static implicit operator Score(int scoreInt)
    {
        return From(scoreInt);
    }

    public static implicit operator int(Score value)
    {
        return value._score;
    }

    public static bool operator ==(Score v1, Score v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(Score v1, Score v2)
    {
        return !v1.Equals(v2);
    }

    public static bool operator >(Score v1, Score v2)
    {
        return v1._score > v2._score;
    }

    public static bool operator <(Score v1, Score v2)
    {
        return v2 > v1;
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case DifficultyLevel other:
                return Equals(other);
            default:
                return false;
        }
    }


    public static bool TryParse(string scoreString, out Score? result)
    {
        if (string.IsNullOrWhiteSpace(scoreString))
        {
            result = null;
            return true;
        }

        var couldParse = TryParse(scoreString, out Score value);
        result = value;
        return couldParse;
    }

    public static bool TryParse(string scoreString, out Score result)
    {
        result = default;
        return int.TryParse(scoreString, out var scoreInt) && TryParse(scoreInt, out result);
    }

    public static bool TryParse(int scoreInt, out Score result)
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


    private bool Equals(Score otherParam)
    {
        return _score == otherParam._score;
    }

    public override int GetHashCode()
    {
        return _score.GetHashCode();
    }

    public static Score From(int score)
    {
        if (score < Min._score) throw new InvalidScoreException("Level must be greater than 0");

        if (score > Max._score) throw new InvalidDifficultyLevelException("Level cannot be greater than 28");

        return new Score(score);
    }
}