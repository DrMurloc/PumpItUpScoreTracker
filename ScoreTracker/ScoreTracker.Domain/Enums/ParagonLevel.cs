using System.ComponentModel;
using System.Reflection;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Enums;

public enum ParagonLevel
{
    None,
    F,

    D,

    C,

    B,

    A,

    [Description("A+")] APlus,

    AA,

    [Description("AA+")] AAPlus,

    AAA,

    [Description("AAA+")] AAAPlus,

    S,

    [Description("S+")] SPlus,

    SS,

    [Description("SS+")] SSPlus,

    SSS,
    [Description("SSS+")] SSSPlus,
    PG
}

public static class ParagonLevelGradeHelperMethods
{
    private static readonly IDictionary<PhoenixLetterGrade, ParagonLevel> _levelDict = Enum
        .GetValues<PhoenixLetterGrade>()
        .ToDictionary(e => e, e => Enum.Parse<ParagonLevel>(e.ToString()));

    public static ParagonLevel GetParagonLevel(this PhoenixScore score)
    {
        return score == 1000000 ? ParagonLevel.PG : _levelDict[score.LetterGrade];
    }

    public static string GetName(this ParagonLevel enumValue)
    {
        return typeof(ParagonLevel).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}