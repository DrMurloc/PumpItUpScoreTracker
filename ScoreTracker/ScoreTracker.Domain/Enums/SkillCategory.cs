using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum SkillCategory
{
    [Color("#D32F2F")] Stamina,
    [Color("#AB47BC")] Gimmick,
    [Color("#1976D2")] Twist,
    [Color("#2E7D32")] Tech
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