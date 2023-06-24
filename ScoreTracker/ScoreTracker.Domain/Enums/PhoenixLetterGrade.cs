using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.Domain.Enums;

public enum PhoenixLetterGrade
{
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
    [Description("SSS+")] SSSPlus
}

public static class PhoenixLetterGradeHelperMethods
{
    public static string GetName(this PhoenixLetterGrade enumValue)
    {
        return typeof(PhoenixLetterGrade).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}