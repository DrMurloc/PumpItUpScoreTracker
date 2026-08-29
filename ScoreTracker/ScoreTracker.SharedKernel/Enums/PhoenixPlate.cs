using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.SharedKernel.Enums;

public enum PhoenixPlate
{
    [PlateShorthandAttribute("RG")] [Description("Rough Game")]
    RoughGame,

    [PlateShorthandAttribute("FG")] [Description("Fair Game")]
    FairGame,

    [PlateShorthandAttribute("TG")] [Description("Talented Game")]
    TalentedGame,

    [PlateShorthandAttribute("MG")] [Description("Marvelous Game")]
    MarvelousGame,

    [PlateShorthandAttribute("SG")] [Description("Superb Game")]
    SuperbGame,

    [PlateShorthandAttribute("EG")] [Description("Extreme Game")]
    ExtremeGame,

    [PlateShorthandAttribute("UG")] [Description("Ultimate Game")]
    UltimateGame,

    [PlateShorthandAttribute("PG")] [Description("Perfect Game")]
    PerfectGame
}

/// <summary>
///     Which judgements a plate counts against itself. A Perfect Game admits no non-perfect at
///     all; a Talented Game admits ten misses and any number of everything else. The axis is
///     part of the rule, not a detail of how it is checked.
/// </summary>
public enum PlateAxis
{
    NonPerfects,
    GoodsBadsAndMisses,
    BadsAndMisses,
    Misses
}

/// <summary>
///     One plate's rule: the judgements it counts, and how many of them it survives.
/// </summary>
public readonly record struct PlateTolerance(PhoenixPlate Plate, PlateAxis Axis, int MaxAllowed)
{
    public int CountIn(int greats, int goods, int bads, int misses)
    {
        return Axis switch
        {
            PlateAxis.NonPerfects => greats + goods + bads + misses,
            PlateAxis.GoodsBadsAndMisses => goods + bads + misses,
            PlateAxis.BadsAndMisses => bads + misses,
            _ => misses
        };
    }

    public bool Tolerates(int greats, int goods, int bads, int misses)
    {
        return CountIn(greats, goods, bads, misses) <= MaxAllowed;
    }
}

[ExcludeFromCodeCoverage]
internal sealed class PlateShorthandAttribute : Attribute
{
    public PlateShorthandAttribute(string shorthand)
    {
        Shorthand = shorthand;
    }

    public string Shorthand { get; }
}

[ExcludeFromCodeCoverage]
public static class PhoenixPlateHelperMethods
{
    private static readonly IDictionary<string, PhoenixPlate> Parser =
        Enum.GetValues<PhoenixPlate>().ToDictionary(e => e.GetName());

    private static readonly IDictionary<string, PhoenixPlate> ShorthandParser = Enum.GetValues<PhoenixPlate>()
        .ToDictionary(e => e.GetShorthand(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Every plate's rule, strictest first, so the first one a run satisfies is the plate it
    ///     earned. Rough Game is absent on purpose: it tolerates everything and is what a run
    ///     falls through to. One table, because two callers ask two questions of the same rule —
    ///     <see cref="ScoreTracker.SharedKernel.Enums.PlateTolerance.Tolerates" /> answers "what
    ///     did this run earn", and the stage-break solver asks which plate a run broke by exactly
    ///     one judgement (docs/design/pass-command-detection.md D32). Written twice they drift.
    /// </summary>
    public static readonly IReadOnlyList<PlateTolerance> Tolerances = new[]
    {
        new PlateTolerance(PhoenixPlate.PerfectGame, PlateAxis.NonPerfects, 0),
        new PlateTolerance(PhoenixPlate.UltimateGame, PlateAxis.GoodsBadsAndMisses, 0),
        new PlateTolerance(PhoenixPlate.ExtremeGame, PlateAxis.BadsAndMisses, 0),
        new PlateTolerance(PhoenixPlate.SuperbGame, PlateAxis.Misses, 0),
        new PlateTolerance(PhoenixPlate.MarvelousGame, PlateAxis.Misses, 5),
        new PlateTolerance(PhoenixPlate.TalentedGame, PlateAxis.Misses, 10),
        new PlateTolerance(PhoenixPlate.FairGame, PlateAxis.Misses, 20)
    };

    public static string GetShorthand(this PhoenixPlate enumValue)
    {
        return typeof(PhoenixPlate).GetField(enumValue.ToString())?.GetCustomAttribute<PlateShorthandAttribute>()
            ?.Shorthand ?? enumValue.ToString();
    }

    public static string GetName(this PhoenixPlate enumValue)
    {
        return typeof(PhoenixPlate).GetField(enumValue.ToString())?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? enumValue.ToString();
    }

    public static PhoenixPlate? TryParse(string? value)
    {
        return value == null ? null : Parser.TryGetValue(value, out var value1) ? value1 : null;
    }

    public static PhoenixPlate ParseShorthand(string value)
    {
        return ShorthandParser[value];
    }
}
