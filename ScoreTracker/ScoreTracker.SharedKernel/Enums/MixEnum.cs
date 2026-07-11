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
    ///     Phoenix-era mixes track numeric 1M-scale scores with plates; everything else
    ///     (XX and older, plus the Infinity/Pro line) uses the legacy model — letter
    ///     grade + broken flag + optional era-scale score (docs/design/legacy-mixes.md).
    /// </summary>
    public static bool UsesLegacyScoring(this MixEnum enumValue)
    {
        return enumValue is not (MixEnum.Phoenix or MixEnum.Phoenix2);
    }

    /// <summary>Primary mixes show directly in the mix picker; the rest live behind "More". Mirrors Mix.IsPrimary.</summary>
    public static bool IsPrimary(this MixEnum enumValue)
    {
        return enumValue is MixEnum.XX or MixEnum.Phoenix or MixEnum.Phoenix2;
    }

    /// <summary>Timeline position, oldest lowest. Mirrors the Mix table's SortOrder seed values.</summary>
    public static int DisplayOrder(this MixEnum enumValue)
    {
        return enumValue switch
        {
            MixEnum.FirstDanceFloor => 10,
            MixEnum.SecondUltimateRemix => 20,
            MixEnum.ThirdObg => 30,
            MixEnum.ObgSeasonEvolution => 40,
            MixEnum.Collection => 50,
            MixEnum.PerfectCollection => 60,
            MixEnum.Extra => 70,
            MixEnum.Premiere => 80,
            MixEnum.Prex => 90,
            MixEnum.Rebirth => 100,
            MixEnum.Premiere2 => 110,
            MixEnum.Prex2 => 120,
            MixEnum.Premiere3 => 130,
            MixEnum.Prex3 => 140,
            MixEnum.Exceed => 150,
            MixEnum.Exceed2 => 160,
            MixEnum.Zero => 170,
            MixEnum.Nx => 180,
            MixEnum.Pro => 185,
            MixEnum.Nx2 => 190,
            MixEnum.NxAbsolute => 200,
            MixEnum.Pro2 => 205,
            MixEnum.Fiesta => 210,
            MixEnum.FiestaEx => 220,
            MixEnum.Fiesta2 => 230,
            MixEnum.Infinity => 235,
            MixEnum.Prime => 240,
            MixEnum.Prime2 => 250,
            MixEnum.XX => 260,
            MixEnum.Phoenix => 270,
            MixEnum.Phoenix2 => 280,
            _ => 0
        };
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
