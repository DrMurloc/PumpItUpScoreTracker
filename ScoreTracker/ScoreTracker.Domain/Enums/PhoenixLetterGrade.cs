using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum PhoenixLetterGrade
{
    [Modifier(0)] C,
    [Modifier(0)] B,
    [Modifier(0)] A,
    [Modifier(.9)] [Description("A+")] APlus,
    [Modifier(1)] AA,
    [Modifier(1.05)] [Description("AA+")] AAPlus,

    [Modifier(1.10)] AAA,

    [Modifier(1.15)] [Description("AAA+")] AAAPlus,

    [Modifier(1.20)] S,
    [Modifier(1.26)] [Description("S+")] SPlus,
    [Modifier(1.32)] SS,

    [Modifier(1.38)] [Description("SS+")] SSPlus,

    [Modifier(1.44)] SSS,

    [Modifier(1.50)] [Description("SSS+")] SSSPlus
}

internal sealed class ModifierAttribute : Attribute
{
    public ModifierAttribute(double modifier)
    {
        Modifier = modifier;
    }

    public double Modifier { get; }
}

public static class PhoenixLetterGradeHelperMethods
{
    private static readonly IDictionary<string, PhoenixLetterGrade> Parser =
        Enum.GetValues<PhoenixLetterGrade>().ToDictionary(e => e.GetName());

    public static double GetModifier(this PhoenixLetterGrade enumValue)
    {
        return typeof(PhoenixLetterGrade).GetField(enumValue.ToString())?.GetCustomAttribute<ModifierAttribute>()?
            .Modifier ?? throw new Exception($"Phoenix letter grade {enumValue} is missing an attribute");
    }

    public static PhoenixLetterGrade? TryParse(string? value)
    {
        return value == null ? null : Parser.ContainsKey(value) ? Parser[value] : null;
    }

    public static string GetName(this PhoenixLetterGrade enumValue)
    {
        return typeof(PhoenixLetterGrade).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}