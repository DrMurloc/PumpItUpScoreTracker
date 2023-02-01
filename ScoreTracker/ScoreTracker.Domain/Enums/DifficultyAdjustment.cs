using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum DifficultyAdjustment
{
    [DifficultyAdjustmentDescription(-4, -2, "2+ Levels Overrated")]
    VeryOverrated,

    [DifficultyAdjustmentDescription(-3, -1, "1 Level Overrated")]
    Overrated,

    [DifficultyAdjustmentDescription(-2, -.5, "Very Easy")]
    VeryEasy,

    [DifficultyAdjustmentDescription(-1, -.25, "Easy")]
    Easy,

    [DifficultyAdjustmentDescription(0, 0, "Medium")]
    Medium,

    [DifficultyAdjustmentDescription(1, .25, "Hard")]
    Hard,

    [DifficultyAdjustmentDescription(2, .5, "Very Hard")]
    VeryHard,

    [DifficultyAdjustmentDescription(3, 1, "1 Level Underrated")]
    Underrated,

    [DifficultyAdjustmentDescription(4, 2, "2+ Levels Underrated")]
    VeryUnderrated
}

public sealed class DifficultyAdjustmentDescriptionAttribute : Attribute
{
    public DifficultyAdjustmentDescriptionAttribute(int scale, double adjustment, string name)
    {
        Adjustment = adjustment;
        Scale = scale;
        Name = name;
    }

    public double Adjustment { get; }
    public int Scale { get; }
    public string Name { get; }
}

public static class DifficultyAdjustmentHelpers
{
    private static readonly IDictionary<int, DifficultyAdjustment> Parser =
        Enum.GetValues<DifficultyAdjustment>().ToDictionary(e => e.GetScale());

    public static DifficultyAdjustment From(int scale)
    {
        return Parser[scale];
    }

    public static string GetDescription(this DifficultyAdjustment enumValue)
    {
        return typeof(DifficultyAdjustment).GetField(enumValue.ToString())
            ?.GetCustomAttribute<DifficultyAdjustmentDescriptionAttribute>()
            ?.Name ?? throw new ArgumentNullException(nameof(DifficultyAdjustmentDescriptionAttribute),
            "Difficulty Scale is missing attribute");
    }

    public static double GetAdjustment(this DifficultyAdjustment enumValue)
    {
        return typeof(DifficultyAdjustment).GetField(enumValue.ToString())
            ?.GetCustomAttribute<DifficultyAdjustmentDescriptionAttribute>()
            ?.Adjustment ?? throw new ArgumentNullException(nameof(DifficultyAdjustmentDescriptionAttribute),
            "Difficulty Scale is missing attribute");
    }

    public static int GetScale(this DifficultyAdjustment enumValue)
    {
        return typeof(DifficultyAdjustment).GetField(enumValue.ToString())
            ?.GetCustomAttribute<DifficultyAdjustmentDescriptionAttribute>()
            ?.Scale ?? throw new ArgumentNullException(nameof(DifficultyAdjustmentDescriptionAttribute),
            "Difficulty Scale is missing attribute");
    }
}