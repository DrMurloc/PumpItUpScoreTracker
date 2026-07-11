using System.ComponentModel;
using System.Reflection;

namespace ScoreTracker.SharedKernel.Enums;

public enum MixEnum
{
    XX,
    Phoenix,

    [Description("Phoenix 2")] Phoenix2,

    // Legacy mixes (docs/design/legacy-mixes.md). Append-only: serialized values
    // must stay stable — display order comes from the Mix table's SortOrder,
    // never from enum order. Prime JE deliberately has no value (folded into
    // Prime); Pro/Pro 2 are the American line, catalogs backfilled separately.
    [Description("The 1st Dance Floor")] FirstDanceFloor,
    [Description("2nd Ultimate Remix")] SecondUltimateRemix,
    [Description("3rd O.B.G")] ThirdObg,
    [Description("The O.B.G / Season Evolution")] ObgSeasonEvolution,
    [Description("The Collection")] Collection,
    [Description("The Perfect Collection")] PerfectCollection,
    Extra,
    [Description("The Premiere")] Premiere,
    [Description("The Prex")] Prex,
    [Description("The Rebirth")] Rebirth,
    [Description("The Premiere 2")] Premiere2,
    [Description("The Prex 2")] Prex2,
    [Description("The Premiere 3")] Premiere3,
    [Description("The Prex 3")] Prex3,
    Exceed,
    [Description("Exceed 2")] Exceed2,
    Zero,
    [Description("NX / New Xenesis")] Nx,
    [Description("NX2 / Next Xenesis")] Nx2,
    [Description("NX Absolute")] NxAbsolute,
    Fiesta,
    [Description("Fiesta EX")] FiestaEx,
    [Description("Fiesta 2")] Fiesta2,
    Prime,
    [Description("Prime 2")] Prime2,
    Infinity,
    Pro,
    [Description("Pro 2")] Pro2
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
    ///     same art as the Discord logo emojis; Phoenix 2's deepened slightly from the
    ///     sampled value for stripe contrast, owner call). The session-snapshot card's
    ///     accent stripe uses this so the mix reads at a glance while several run in
    ///     parallel.
    /// </summary>
    public static uint GetAccentColor(this MixEnum enumValue)
    {
        return enumValue switch
        {
            MixEnum.Phoenix => 0x1D9BCCu,
            MixEnum.Phoenix2 => 0x6CA832u,
            MixEnum.XX => 0xD49D3Bu,
            _ => 0x6E8CA0u
        };
    }
}
