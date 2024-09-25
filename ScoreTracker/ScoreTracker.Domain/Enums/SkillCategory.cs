using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum SkillCategory
{
    [Color("#D32F2F")] Speed,
    [Color("#F57C00")] Stamina,
    [Color("#388E3C")] Twist,
    [Color("#1976D2")] Bracket,
    [Color("#7B1FA2")] Tech
}

public sealed class ColorAttribute : Attribute
{
    public string Color { get; }

    public ColorAttribute(string color)
    {
        Color = color;
    }
}

public static class SkillCategoryHelpers
{
    private static readonly IDictionary<SkillCategory, string> Colors = Enum.GetValues<SkillCategory>()
        .ToDictionary(c => c, c => typeof(SkillCategory).GetField(c.ToString())
            ?.GetCustomAttribute<ColorAttribute>()
            ?.Color ?? "");

    public static string GetColor(this SkillCategory category)
    {
        return Colors[category];
    }
}