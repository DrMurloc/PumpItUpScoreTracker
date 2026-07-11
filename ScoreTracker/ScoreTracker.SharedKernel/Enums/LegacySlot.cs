using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     Pre-Exceed chart slot names (docs/design/legacy-mixes.md). In those eras the
///     slot is part of a chart's identity — the same song can carry Hard 5 AND
///     Crazy 5 — and the numeric ratings live on a per-era scale that does NOT
///     translate to modern levels, so UI must never render "Crazy 7" as "S7".
///     "Another" is the era's modifier for alternate versions of a slot. The values
///     are exactly the combinations observed in the pumpout label data.
/// </summary>
public enum LegacySlot
{
    Easy,
    Normal,
    Hard,
    Crazy,
    Freestyle,
    Nightmare,
    Practice,
    Another,
    [Description("Another Normal")] AnotherNormal,
    [Description("Another Hard")] AnotherHard,
    [Description("Another Crazy")] AnotherCrazy,
    [Description("Another Freestyle")] AnotherFreestyle,
    [Description("Another Nightmare")] AnotherNightmare
}

[ExcludeFromCodeCoverage]
public static class LegacySlotHelperMethods
{
    public static string GetName(this LegacySlot enumValue)
    {
        return typeof(LegacySlot).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }

    /// <summary>Parses the stored form — either the enum name or the spaced display name ("Another Crazy").</summary>
    public static LegacySlot ParseLegacySlot(string value)
    {
        var collapsed = value.Replace(" ", "");
        if (Enum.TryParse<LegacySlot>(collapsed, true, out var parsed)) return parsed;
        throw new ArgumentException($"Invalid legacy slot {value}", nameof(value));
    }

    public static bool TryParseLegacySlot(string? value, out LegacySlot slot)
    {
        slot = default;
        return value is not null && Enum.TryParse(value.Replace(" ", ""), true, out slot);
    }
}
