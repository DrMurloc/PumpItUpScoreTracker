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

    /// <summary>
    ///     The mix's brand color as 0xRRGGBB — sampled from the official mix logos (the
    ///     same art as the Discord logo emojis). The session-snapshot card's accent
    ///     stripe uses this so the mix reads at a glance while several run in parallel.
    /// </summary>
    public static uint GetAccentColor(this MixEnum enumValue)
    {
        return enumValue switch
        {
            MixEnum.Phoenix => 0x1D9BCCu,
            MixEnum.Phoenix2 => 0x81B644u,
            MixEnum.XX => 0xD49D3Bu,
            _ => 0x6E8CA0u
        };
    }
}
