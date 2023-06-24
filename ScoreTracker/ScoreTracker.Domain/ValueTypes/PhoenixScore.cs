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

    public PhoenixLetterGrade LetterGrade => _score switch
    {
        >= 995000 => PhoenixLetterGrade.SSSPlus,
        >= 990000 => PhoenixLetterGrade.SSS,
        >= 985000 => PhoenixLetterGrade.SSPlus,
        >= 980000 => PhoenixLetterGrade.SS,
        >= 975000 => PhoenixLetterGrade.SPlus,
        >= 970000 => PhoenixLetterGrade.S,
        >= 960000 => PhoenixLetterGrade.AAAPlus,
        >= 950000 => PhoenixLetterGrade.AAA,
        >= 925000 => PhoenixLetterGrade.AAPlus,
        >= 900000 => PhoenixLetterGrade.AA,
        >= 800000 => PhoenixLetterGrade.APlus,
        >= 750000 => PhoenixLetterGrade.A,
        >= 700000 => PhoenixLetterGrade.B,
        _ => PhoenixLetterGrade.C
    };

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
}