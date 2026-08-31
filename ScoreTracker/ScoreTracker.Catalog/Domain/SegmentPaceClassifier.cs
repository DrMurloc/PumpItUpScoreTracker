namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Names a segment's pace against its FOLDER, and by FOOT SPEED, not throughput (owner
///     rulings 2026-08-30, twice): "NPS doesn't mean anything", and section eNPS then marked
///     Horang Pungryuga's long unbroken runs Very Fast while missing Solve My Hurt's real
///     16th-drill bursts entirely — a section average rewards continuity, not quick feet. The
///     measure is the segment's <b>burst rate</b>: the fastest <see cref="BurstWindowRows" />
///     consecutive judgement rows anywhere in it, in rows per second — the cadence the feet
///     actually hit. Above the folder's P90 is "Very Fast", above P75 "Fast", below P10
///     "Very Slow", below P25 "Slow", and the middle half says nothing. The folder is the
///     snapshot's own (meter, steps type); both banking paths run this as a whole-corpus
///     post-pass, which is what lets a Reprocess press re-stamp pace without an upload. A
///     folder with fewer than <see cref="MinimumFolderSample" /> measured segments stamps
///     nothing: a chart alone in its folder would only ever be fast relative to itself.
/// </summary>
internal static class SegmentPaceClassifier
{
    private const int MinimumFolderSample = 12;
    private const int BurstWindowRows = 8;

    public const string VeryFast = "vf";
    public const string Fast = "f";
    public const string Slow = "s";
    public const string VerySlow = "vs";

    public static IReadOnlyDictionary<Guid, EnrichedStepChart> Stamp(
        IReadOnlyDictionary<Guid, EnrichedStepChart> charts)
    {
        var burstsByChart = charts.ToDictionary(kv => kv.Key, kv => Bursts(kv.Value));

        var cutoffsByFolder = charts
            .Where(kv => kv.Value.Meter != null && kv.Value.StepsType != null)
            .GroupBy(kv => (Meter: kv.Value.Meter!.Value, Type: kv.Value.StepsType!))
            .ToDictionary(g => g.Key, g => Cutoffs(g
                .SelectMany(kv => burstsByChart[kv.Key])
                .Where(b => b != null)
                .Select(b => b!.Value)
                .OrderBy(v => v)
                .ToArray()));

        return charts.ToDictionary(kv => kv.Key, kv =>
        {
            var chart = kv.Value;
            if (chart.Meter == null || chart.StepsType == null) return chart;
            var cutoffs = cutoffsByFolder[(chart.Meter.Value, chart.StepsType)];
            if (cutoffs == null) return chart;
            var bursts = burstsByChart[kv.Key];
            return chart with
            {
                Segments = chart.Segments
                    .Select((s, i) => s with { Pace = PaceOf(bursts[i], cutoffs.Value) })
                    .ToArray()
            };
        });
    }

    private static decimal?[] Bursts(EnrichedStepChart chart)
    {
        var times = chart.Rows.Select(r => r.Time).ToArray();
        return chart.Segments.Select(s => Burst(times, s.Start, s.End)).ToArray();
    }

    /// <summary>
    ///     The fastest window of <see cref="BurstWindowRows" /> consecutive rows inside
    ///     [start, end), in rows per second. A section too short for a full window measures
    ///     its whole span; fewer than two rows measures nothing. Row times arrive ascending —
    ///     the enricher builds them that way and the payload preserves the order.
    /// </summary>
    internal static decimal? Burst(IReadOnlyList<decimal> orderedRowTimes, decimal start, decimal end)
    {
        var inside = new List<decimal>();
        foreach (var time in orderedRowTimes)
        {
            if (time < start) continue;
            if (time >= end) break;
            inside.Add(time);
        }

        if (inside.Count < 2) return null;
        if (inside.Count <= BurstWindowRows)
        {
            var span = inside[^1] - inside[0];
            return span > 0 ? (inside.Count - 1) / span : null;
        }

        decimal best = 0;
        for (var i = 0; i + BurstWindowRows <= inside.Count; i++)
        {
            var span = inside[i + BurstWindowRows - 1] - inside[i];
            if (span > 0) best = Math.Max(best, (BurstWindowRows - 1) / span);
        }

        return best > 0 ? best : null;
    }

    private static (decimal P10, decimal P25, decimal P75, decimal P90)? Cutoffs(decimal[] sorted)
    {
        if (sorted.Length < MinimumFolderSample) return null;
        return (Rank(sorted, 0.10m), Rank(sorted, 0.25m), Rank(sorted, 0.75m), Rank(sorted, 0.90m));
    }

    /// <summary>Nearest-rank percentile — deterministic, no interpolation to argue about.</summary>
    private static decimal Rank(decimal[] sorted, decimal percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static string? PaceOf(decimal? burst, (decimal P10, decimal P25, decimal P75, decimal P90) cutoffs)
    {
        if (burst == null) return null;
        if (burst > cutoffs.P90) return VeryFast;
        if (burst > cutoffs.P75) return Fast;
        if (burst < cutoffs.P10) return VerySlow;
        if (burst < cutoffs.P25) return Slow;
        return null;
    }
}
