using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     How fast a chart is FOR ITS FOLDER (docs/design/chart-identity.md §2). A folder's own
///     notes-per-second distribution is cut at ±0.5σ and ±1.5σ, so "Fast" means fast next to
///     the other charts on that level rather than against some absolute number that would put
///     every 24 in one band and every 17 in another.
///     <para>
///         The bands are stored as tier-list categories because that is what the tier-list
///         family stores, but nothing here is a difficulty judgement: a slow chart is not an
///         easy one, and the folder's own pass rates routinely say the opposite.
///     </para>
/// </summary>
internal static class SpeedBands
{
    /// <summary>The stored list name — the Popularity precedent, not published through api/v2.</summary>
    public const string ListName = "Speed";

    /// <summary>
    ///     A band this small is noise rather than a reading, and it strands its charts in a
    ///     section that says nothing. Below this it folds into its inward neighbour — Very Fast
    ///     into Fast, Very Slow into Slow.
    /// </summary>
    public const int MinimumBandSize = 4;

    /// <summary>
    ///     Slowest to fastest. Categories are borrowed for their ORDER, which is why the
    ///     slowest band takes the category the tier lists draw at the "easy" end.
    /// </summary>
    private static readonly TierListCategory[] Ladder =
    {
        TierListCategory.Overrated, TierListCategory.VeryEasy, TierListCategory.Medium,
        TierListCategory.Hard, TierListCategory.VeryHard
    };

    /// <summary>
    ///     Bands one folder. Charts arrive as (chart, nps) and come back with the band they
    ///     landed in; a folder of fewer than two charts has no distribution to cut and gets
    ///     nothing, because a lone chart is not fast or slow relative to anything.
    /// </summary>
    public static IReadOnlyList<SongTierListEntry> Band(IReadOnlyCollection<(Guid ChartId, decimal Nps)> charts)
    {
        if (charts.Count < 2) return Array.Empty<SongTierListEntry>();

        var values = charts.Select(c => (double)c.Nps).ToArray();
        var mean = values.Average();
        var deviation = StandardDeviation(values, mean);
        // Every chart in the folder runs at the same speed: there is no spread to band on, and
        // dividing by zero would put everything in one arbitrary band with false confidence.
        if (deviation <= 0) return Array.Empty<SongTierListEntry>();

        var indexed = charts
            .Select(c => (c.ChartId, c.Nps, Band: RawBand(((double)c.Nps - mean) / deviation)))
            .ToArray();
        var merged = MergeThinBands(indexed.Select(c => c.Band).ToArray());

        return indexed
            .Select(c => new SongTierListEntry(ListName, c.ChartId, Ladder[merged[c.Band]],
                // The measurement itself rides along, so a reader can print the real number
                // beside the band name without a second lookup.
                (int)Math.Round(c.Nps * 100)))
            .ToArray();
    }

    private static int RawBand(double z)
    {
        return z switch
        {
            < -1.5 => 0,
            < -0.5 => 1,
            <= 0.5 => 2,
            <= 1.5 => 3,
            _ => 4
        };
    }

    /// <summary>
    ///     Folds a thin TAIL into the nearest occupied band inward of it. Only the two extreme
    ///     bands are ever folded, and only once each: they are what a handful of outliers
    ///     strands, and cascading further would eat a small folder's whole structure — ten
    ///     charts have thin bands everywhere, and collapsing them all into one section says
    ///     less than the bands did.
    ///     <para>
    ///         "Nearest occupied" rather than "adjacent": a folder packed tight around its mean
    ///         with two outliers leaves the bands between them empty, and folding into an empty
    ///         one would leave the outliers exactly as alone under a different name.
    ///     </para>
    /// </summary>
    private static IReadOnlyDictionary<int, int> MergeThinBands(IReadOnlyCollection<int> bands)
    {
        var counts = Enumerable.Range(0, Ladder.Length).ToDictionary(b => b, b => bands.Count(x => x == b));
        var target = Enumerable.Range(0, Ladder.Length).ToDictionary(b => b, b => b);

        var top = Ladder.Length - 1;
        if (counts[top] > 0 && counts[top] < MinimumBandSize)
        {
            var into = Enumerable.Range(0, top).LastOrDefault(b => counts[b] > 0, -1);
            if (into >= 0)
            {
                target[top] = into;
                counts[into] += counts[top];
                counts[top] = 0;
            }
        }

        if (counts[0] > 0 && counts[0] < MinimumBandSize)
        {
            var into = Enumerable.Range(1, top).FirstOrDefault(b => counts[b] > 0, -1);
            if (into >= 0) target[0] = into;
        }

        return target;
    }

    private static double StandardDeviation(IReadOnlyCollection<double> values, double mean)
    {
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }
}
