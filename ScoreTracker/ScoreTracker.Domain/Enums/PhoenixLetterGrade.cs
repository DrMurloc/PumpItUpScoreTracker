using System.ComponentModel;
using System.Reflection;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Enums;

public enum PhoenixLetterGrade
{
    [ScoreRange(0, 449999)] [Modifier(.4)] F,

    [ScoreRange(450000, 549999)] [Modifier(.5)]
    D,

    [ScoreRange(550000, 649999)] [Modifier(.6)]
    C,

    [ScoreRange(650000, 749999)] [Modifier(.7)]
    B,

    [ScoreRange(750000, 824999)] [Modifier(.8)]
    A,

    [ScoreRange(825000, 899999)] [Modifier(.9)] [Description("A+")]
    APlus,

    [ScoreRange(900000, 924999)] [Modifier(1)]
    AA,

    [ScoreRange(925000, 949999)] [Modifier(1.05)] [Description("AA+")]
    AAPlus,

    [ScoreRange(950000, 959999)] [Modifier(1.10)]
    AAA,

    [ScoreRange(960000, 969999)] [Modifier(1.15)] [Description("AAA+")]
    AAAPlus,

    [ScoreRange(970000, 974999)] [Modifier(1.20)]
    S,

    [ScoreRange(975000, 979999)] [Modifier(1.26)] [Description("S+")]
    SPlus,

    [ScoreRange(980000, 984999)] [Modifier(1.32)]
    SS,

    [ScoreRange(985000, 989999)] [Modifier(1.38)] [Description("SS+")]
    SSPlus,

    [ScoreRange(990000, 994999)] [Modifier(1.44)]
    SSS,

    [ScoreRange(995000, 1000000)] [Modifier(1.50)] [Description("SSS+")]
    SSSPlus
}

internal sealed class ScoreRangeAttribute : Attribute
{
    public ScoreRangeAttribute(int minimumScore, int maximumScore)
    {
        MinimumScore = minimumScore;
        MaximumScore = maximumScore;
    }

    public PhoenixScore MinimumScore { get; }
    public PhoenixScore MaximumScore { get; }
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
    private static readonly IDictionary<PhoenixLetterGrade, ScoreRangeAttribute> CachedRanges = Enum
        .GetValues<PhoenixLetterGrade>()
        .ToDictionary(g => g,
            g => typeof(PhoenixLetterGrade).GetField(g.ToString())?.GetCustomAttribute<ScoreRangeAttribute>() ??
                 throw new Exception($"Score Range not set up for {g}"));

    private static readonly IDictionary<string, PhoenixLetterGrade> Parser =
        Enum.GetValues<PhoenixLetterGrade>().ToDictionary(e => e.GetName());

    public static PhoenixScore GetMinimumScore(this PhoenixLetterGrade letterGrade)
    {
        return CachedRanges[letterGrade].MinimumScore;
    }

    public static PhoenixScore GetMaximumScore(this PhoenixLetterGrade letterGrade)
    {
        return CachedRanges[letterGrade].MaximumScore;
    }

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