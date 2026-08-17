using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     One band of merged-pool totals the population section groups players into.
///     <see cref="Ceiling" /> is where the next band starts (exclusive); null on the top band.
/// </summary>
internal sealed record PumbilityPoolBand(string Key, string? Title, double Floor, double? Ceiling)
{
    public bool Contains(double total)
    {
        return total >= Floor && (Ceiling == null || total < Ceiling);
    }
}

/// <summary>
///     Which bands a mix's population is read in (docs/design/pumbility-calculator.md D9).
///     <para>
///         Phoenix 2 has a named total-PUMBILITY ladder — BRONZE at 10,000 up to ABYSS ABSOLUTE at
///         20,000 — so its bands are the gems themselves, read off <see cref="Phoenix2PumbilityLevel" />
///         so a threshold can never differ from the one the titles use; a pool below BRONZE lands in an
///         unranked band. Phoenix has no PUMBILITY rungs, so it takes eight uneven bands of the total:
///         everything under 20,000, then a band per ten thousand, then 80,000 and up. Uneven on
///         purpose — the shift the data shows sits around 60,000, and a uniform width fine enough to
///         see it would be forty rows.
///     </para>
/// </summary>
internal static class PumbilityPoolBands
{
    /// <summary>
    ///     Fewer players than this and a band is not drawn — a bar built on one or two people is a
    ///     picture of them, not of the rung. The record still carries the band, so the page can say
    ///     "not enough players yet" instead of pretending the rung is empty. One number, on the contract.
    /// </summary>
    public const int MinimumPlayers = Contracts.PumbilityPoolCompositionRecord.MinimumPlayersToDraw;

    /// <summary>The key of the Phoenix 2 band below BRONZE.</summary>
    public const string Phoenix2Unranked = "unranked";

    private static readonly IReadOnlyList<PumbilityPoolBand> PhoenixBands = BuildPhoenixBands();
    private static readonly IReadOnlyList<PumbilityPoolBand> Phoenix2Bands = BuildPhoenix2Bands();

    /// <summary>The bands for a mix, lowest first. Empty for a mix without a PUMBILITY formula.</summary>
    public static IReadOnlyList<PumbilityPoolBand> For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.Phoenix => PhoenixBands,
            MixEnum.Phoenix2 => Phoenix2Bands,
            _ => Array.Empty<PumbilityPoolBand>()
        };
    }

    /// <summary>The band a merged-pool total falls in, or null for a mix with no bands.</summary>
    public static PumbilityPoolBand? BandFor(MixEnum mix, double total)
    {
        return For(mix).FirstOrDefault(b => b.Contains(total));
    }

    private static IReadOnlyList<PumbilityPoolBand> BuildPhoenixBands()
    {
        var bands = new List<PumbilityPoolBand> { new("lt20k", null, 0, 20_000) };
        for (var floor = 20_000; floor < 80_000; floor += 10_000)
            bands.Add(new PumbilityPoolBand($"{floor / 1000}k", null, floor, floor + 10_000));
        bands.Add(new PumbilityPoolBand("80k+", null, 80_000, null));
        return bands;
    }

    private static IReadOnlyList<PumbilityPoolBand> BuildPhoenix2Bands()
    {
        // A gem starts at its level 1 (the capstone has no levels and starts at its own index).
        var gems = Phoenix2PumbilityLevel.All
            .Where(r => r.IsRanked && (r.Level == 1 || r.IsCapstone))
            .ToArray();
        var bands = new List<PumbilityPoolBand>
        {
            new(Phoenix2Unranked, null, 0, gems[0].Threshold)
        };
        for (var i = 0; i < gems.Length; i++)
        {
            var name = gems[i].Gem!.Value.ToString();
            bands.Add(new PumbilityPoolBand(name, name, gems[i].Threshold,
                i + 1 < gems.Length ? gems[i + 1].Threshold : null));
        }

        return bands;
    }
}
