namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Names a segment's pace against its FOLDER, not against numbers (owner, 2026-08-30 —
///     "NPS doesn't mean anything"): a section's eNPS above the folder's P90 is "Very Fast",
///     above P75 "Fast", below P10 "Very Slow", below P25 "Slow", and the middle half says
///     nothing. The folder is the snapshot's own (meter, steps type) — the same scale the
///     per-segment levels live on — and the distribution is every segment's eNPS across the
///     folder's charts. Both banking paths run this as a whole-corpus post-pass (the ingest
///     over the upload, the reprocess over the archive), which is what lets a Reprocess press
///     re-stamp pace without an upload. A folder with fewer than
///     <see cref="MinimumFolderSample" /> segments stamps nothing: a chart alone in its folder
///     would only ever be fast relative to itself.
/// </summary>
internal static class SegmentPaceClassifier
{
    private const int MinimumFolderSample = 12;

    public const string VeryFast = "vf";
    public const string Fast = "f";
    public const string Slow = "s";
    public const string VerySlow = "vs";

    public static IReadOnlyDictionary<Guid, EnrichedStepChart> Stamp(
        IReadOnlyDictionary<Guid, EnrichedStepChart> charts)
    {
        var cutoffsByFolder = charts.Values
            .Where(c => c.Meter != null && c.StepsType != null)
            .GroupBy(c => (Meter: c.Meter!.Value, Type: c.StepsType!))
            .ToDictionary(g => g.Key, g => Cutoffs(g
                .SelectMany(c => c.Segments)
                .Where(s => s.Enps != null)
                .Select(s => s.Enps!.Value)
                .OrderBy(v => v)
                .ToArray()));

        return charts.ToDictionary(kv => kv.Key, kv =>
        {
            var chart = kv.Value;
            if (chart.Meter == null || chart.StepsType == null) return chart;
            var cutoffs = cutoffsByFolder[(chart.Meter.Value, chart.StepsType)];
            if (cutoffs == null) return chart;
            return chart with
            {
                Segments = chart.Segments
                    .Select(s => s with { Pace = PaceOf(s.Enps, cutoffs.Value) })
                    .ToArray()
            };
        });
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

    private static string? PaceOf(decimal? enps, (decimal P10, decimal P25, decimal P75, decimal P90) cutoffs)
    {
        if (enps == null) return null;
        if (enps > cutoffs.P90) return VeryFast;
        if (enps > cutoffs.P75) return Fast;
        if (enps < cutoffs.P10) return VerySlow;
        if (enps < cutoffs.P25) return Slow;
        return null;
    }
}
