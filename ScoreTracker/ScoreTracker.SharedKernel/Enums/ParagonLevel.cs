using System.ComponentModel;
using System.Reflection;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Enums;

public enum ParagonLevel
{
    None,
    [Description("Paragon")] F,

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

[ExcludeFromCodeCoverage]
public static class ParagonLevelGradeHelperMethods
{
    private static readonly IDictionary<PhoenixLetterGrade, ParagonLevel> _levelDict = Enum
        .GetValues<PhoenixLetterGrade>()
        .ToDictionary(e => e, e => Enum.Parse<ParagonLevel>(e.ToString()));

    private static readonly IDictionary<ParagonLevel, PhoenixLetterGrade> _gradeDict = Enum
        .GetValues<PhoenixLetterGrade>()
        .ToDictionary(e => Enum.Parse<ParagonLevel>(e.ToString()), e => e);

    private static readonly IDictionary<string, ParagonLevel> _letterNames = Enum.GetValues<ParagonLevel>()
        .ToDictionary(e => e.GetName(), e => e, StringComparer.OrdinalIgnoreCase);

    public static ParagonLevel GetParagonLevel(string name)
    {
        return _letterNames[name];
    }

    public static ParagonLevel GetParagonLevel(this PhoenixScore score, MixEnum mix)
    {
        return score == 1000000 ? ParagonLevel.PG : _levelDict[score.LetterGradeFor(mix)];
    }

    public static PhoenixScore MinThreshold(this ParagonLevel level, MixEnum mix)
    {
        return level == ParagonLevel.PG ? PhoenixScore.Max : level.GetLetterGrade().GetMinimumScoreFor(mix);
    }

    public static ParagonLevel GetParagonLevel(this PhoenixLetterGrade letterGrade)
    {
        return _levelDict[letterGrade];
    }

    public static PhoenixLetterGrade GetLetterGrade(this ParagonLevel paragonLevel)
    {
        return _gradeDict[paragonLevel];
    }

    public static string GetName(this ParagonLevel enumValue)
    {
        return typeof(ParagonLevel).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}
