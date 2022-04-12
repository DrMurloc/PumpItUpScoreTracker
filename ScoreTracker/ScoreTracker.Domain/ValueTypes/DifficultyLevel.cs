using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct DifficultyLevel
{
    private readonly int _level;

    private DifficultyLevel(int levelInt)
    {
        _level = levelInt;
    }

    public static readonly DifficultyLevel Max = new(28);
    public static readonly DifficultyLevel Min = new(1);

    public override string ToString()
    {
        return _level.ToString();
    }

    public static implicit operator DifficultyLevel(int levelInt)
    {
        return From(levelInt);
    }

    public static implicit operator int(DifficultyLevel value)
    {
        return value._level;
    }

    public static bool operator ==(DifficultyLevel v1, DifficultyLevel v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(DifficultyLevel v1, DifficultyLevel v2)
    {
        return !v1.Equals(v2);
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


    public static bool TryParse(string levelString, out DifficultyLevel result)
    {
        if (!int.TryParse(levelString, out var levelInt))
            throw new InvalidDifficultyLevelException($"{levelString} is not a valid number");

        return TryParse(levelInt, out result);
    }

    public static bool TryParse(int levelInt, out DifficultyLevel result)
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


    private bool Equals(DifficultyLevel otherParam)
    {
        return _level == otherParam._level;
    }

    public override int GetHashCode()
    {
        return _level.GetHashCode();
    }

    public static DifficultyLevel From(int level)
    {
        if (level < Min) throw new InvalidDifficultyLevelException("Level must be greater than 0");

        if (level > Max) throw new InvalidDifficultyLevelException("Level cannot be greater than 28");

        return new DifficultyLevel(level);
    }
}