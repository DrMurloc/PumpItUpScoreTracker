using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct StepCount
{
    private readonly int _level;

    private StepCount(int levelInt)
    {
        _level = levelInt;
    }


    public override string ToString()
    {
        return _level.ToString();
    }

    public static implicit operator StepCount(int levelInt)
    {
        return From(levelInt);
    }

    public static implicit operator int(StepCount value)
    {
        return value._level;
    }

    public static bool operator ==(StepCount v1, StepCount v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(StepCount v1, StepCount v2)
    {
        return !v1.Equals(v2);
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case StepCount other:
                return Equals(other);
            default:
                return false;
        }
    }


    public static bool TryParse(string levelString, out StepCount result)
    {
        result = default;
        return int.TryParse(levelString, out var levelInt) && TryParse(levelInt, out result);
    }

    public static bool TryParse(int levelInt, out StepCount result)
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


    private bool Equals(StepCount otherParam)
    {
        return _level == otherParam._level;
    }

    public override int GetHashCode()
    {
        return _level.GetHashCode();
    }

    public static StepCount From(int level)
    {
        if (level < 0) throw new InvalidStepCountException("Level must be greater than 0");

        return new StepCount(level);
    }
}