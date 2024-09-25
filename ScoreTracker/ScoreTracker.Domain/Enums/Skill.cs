using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum Skill
{
    [Name("Very Fast")] [Description("Very fast patterns")] [SkillCategory(SkillCategory.Speed)]
    VeryFast,

    [Name("Fast")] [Description("Fast patterns")] [SkillCategory(SkillCategory.Speed)]
    Fast,

    [Name("Moderate")] [Description("Moderately paced patterns")] [SkillCategory(SkillCategory.Speed)]
    Moderate,

    [Name("Slow")] [Description("Slow patterns")] [SkillCategory(SkillCategory.Stamina)]
    Slow,

    [Name("End Run")] [Description("Ends on a large run")] [SkillCategory(SkillCategory.Stamina)]
    EndRun,

    [Name("Stamina")] [Description("High stamina requirement")] [SkillCategory(SkillCategory.Stamina)]
    Stamina,

    [Name("Twists")] [Description("Twisty Patterns")] [SkillCategory(SkillCategory.Twist)]
    Twists,

    [Name("Technical")] [Description("Has unique, uncommon, or chaotic footwork")] [SkillCategory(SkillCategory.Tech)]
    Technical,

    [Name("Brackets")] [Description("Bracket heavy patterns")] [SkillCategory(SkillCategory.Bracket)]
    Brackets,

    [Name("Jumps")] [Description("Jumping patterns")] [SkillCategory(SkillCategory.Tech)]
    Jumps,

    [Name("Bursts")]
    [Description("Patterns that maintain very high speeds for short  periods of time")]
    [SkillCategory(SkillCategory.Speed)]
    Bursts,

    [Name("Drills")] [Description("Drills")] [SkillCategory(SkillCategory.Stamina)]
    Drills,

    [Name("Brackets & Runs")]
    [Description("Charts featuring both sustained runs and brackets")]
    [SkillCategory(SkillCategory.Bracket)]
    BracketsAndRuns
}

public sealed class NameAttribute : Attribute
{
    public string Name { get; }

    public NameAttribute(string name)
    {
        Name = name;
    }
}

public sealed class SkillCategoryAttribute : Attribute
{
    public SkillCategory[] Categories { get; }

    public SkillCategoryAttribute(params SkillCategory[] categories)
    {
        Categories = categories;
    }
}

public static class SkillHelpers
{
    private static readonly IDictionary<Skill, SkillCategory[]> Categories = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<SkillCategoryAttribute>()
            ?.Categories ?? Array.Empty<SkillCategory>());

    private static readonly IDictionary<Skill, string> Colors =
        Categories.ToDictionary(kv => kv.Key, kv => kv.Value.Cast<SkillCategory?>().FirstOrDefault()?.GetColor()??"#333333");

    private static readonly IDictionary<Skill, string> Names = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<NameAttribute>()
            ?.Name ?? "");

    private static readonly IDictionary<Skill, string> Descriptions = Enum.GetValues<Skill>()
        .ToDictionary(c => c, c => typeof(Skill).GetField(c.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? "");

    public static string GetDescription(this Skill skill)
    {
        return Descriptions[skill];
    }

    public static string GetColor(this Skill skill)
    {
        return Colors?[skill]??"";
    }

    public static string GetName(this Skill skill)
    {
        return Names[skill];
    }
}