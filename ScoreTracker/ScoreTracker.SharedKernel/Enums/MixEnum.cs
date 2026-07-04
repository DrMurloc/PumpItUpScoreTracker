using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.SharedKernel.Enums;

public enum MixEnum
{
    XX,
    Phoenix,

    [Description("Phoenix 2")] Phoenix2
}

[ExcludeFromCodeCoverage]
public static class MixEnumHelperMethods
{
    public static string GetName(this MixEnum enumValue)
    {
        return typeof(MixEnum).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }
}
