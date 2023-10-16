using System.Text.RegularExpressions;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct DifficultyLevel : IComparable<DifficultyLevel>
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
        result = default;
        return int.TryParse(levelString, out var levelInt) && TryParse(levelInt, out result);
    }

    public static bool IsValid(int levelInt)
    {
        return TryParse(levelInt, out _);
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

    private static readonly Regex _shortHandRegex = new(@"^\s*([A-Za-z]+)([0-9]+)\s*$", RegexOptions.Compiled);

    public static bool TryParseShortHand(string shortHand, out ChartType chartType, out DifficultyLevel level)
    {
        try
        {
            (chartType, level) = ParseShortHand(shortHand);
            return true;
        }
        catch
        {
            chartType = ChartType.Single;
            level = 1;
            return false;
        }
    }

    public static string ToShorthand(ChartType chartType, DifficultyLevel level)
    {
        return $"{chartType.GetShortHand()}{level}";
    }

    public static (ChartType chartType, DifficultyLevel level) ParseShortHand(string shortHand)
    {
        var match = _shortHandRegex.Match(shortHand);
        if (!match.Success)
            throw new InvalidDifficultyLevelException($"Difficulty Level short hand {shortHand} was invalid");

        var chartType = ChartTypeHelperMethods.ParseChartTypeShortHand(match.Groups[1].Value);
        if (!TryParse(match.Groups[2].Value, out var level))
            throw new InvalidDifficultyLevelException($"{match.Groups[2]} is not a valid level");
        return (chartType, level);
    }

    public int CompareTo(DifficultyLevel other)
    {
        return _level.CompareTo(other._level);
    }

    public int BaseRating => _level < 10 ? 0 : 100 + 5 * (_level - 10) * (_level - 9);

    public static readonly IEnumerable<DifficultyLevel> All = Enumerable.Range(Min, Max)
        .Select(i => (DifficultyLevel)i);
}