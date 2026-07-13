using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     One catalog chart in scope, joined to the player's best attempt. The C3 read seam
///     produces one of these per chart in the selected mix (unplayed charts included —
///     Completion counts over the whole folder). Metric values are set only when the chart
///     is CLEARED: a fail has no meaningful grade/plate/clear-score. <see cref="IsPlayed" />
///     drives the footer/empty-state only; the aggregations key off <see cref="IsPassed" />.
///     Type is normalized to Single / Double / CoOp (performance charts fold into their base).
///     Bucket is the difficulty level, or the player count for co-op.
/// </summary>
public sealed record BreakdownRecord(
    ChartType Type,
    int Bucket,
    bool IsPlayed,
    bool IsPassed,
    int? Score,
    int? GradeRank,
    int? PlateRank);

/// <summary>Ordered category labels for the active mix's scales (worst → best).</summary>
public sealed record BreakdownScales(
    IReadOnlyList<string> GradeNames,
    IReadOnlyList<string> PlateNames);

public enum BreakdownChartKind
{
    Empty,
    Lines,
    StackedBars
}

/// <summary>How the render component resolves a series/segment to a literal hex.</summary>
public enum SeriesColorRole
{
    /// <summary>Qualitative palette by <see cref="SeriesColor.Index" /> (stats, thresholds).</summary>
    Qualitative,

    /// <summary>Rarity ramp at <see cref="SeriesColor.RarityFraction" /> (grade/plate segments).</summary>
    Rarity,

    /// <summary>The "passed / cleared" success color.</summary>
    Pass,

    /// <summary>Muted remainder (unplayed / not-cleared / unpassed).</summary>
    Muted
}

public readonly record struct SeriesColor(SeriesColorRole Role, int Index, double RarityFraction)
{
    public static SeriesColor Qualitative(int index) => new(SeriesColorRole.Qualitative, index, 0);
    public static SeriesColor Rarity(double fraction) => new(SeriesColorRole.Rarity, 0, fraction);
    public static readonly SeriesColor Pass = new(SeriesColorRole.Pass, 0, 0);
    public static readonly SeriesColor Muted = new(SeriesColorRole.Muted, 0, 0);
}

/// <summary>
///     One line (Distribution / Completion) or one stacked segment (Breakdown). Values align
///     to <see cref="BreakdownResult.Buckets" /> (null = gap / no data in that bucket).
/// </summary>
public sealed record BreakdownSeries(
    string Label,
    SeriesColor Color,
    bool Dashed,
    ChartType? Type,
    IReadOnlyList<double?> Values);

/// <summary>A shaded band between two series (Distribution, Score only).</summary>
public sealed record BreakdownBandArea(
    SeriesColor Color,
    IReadOnlyList<double?> Lower,
    IReadOnlyList<double?> Upper);

/// <summary>The render-ready shape — numbers + labels + color roles, no literal colors.</summary>
public sealed record BreakdownResult(
    BreakdownChartKind Kind,
    IReadOnlyList<int> Buckets,
    string XAxisTitle,
    string YAxisTitle,
    IReadOnlyList<BreakdownSeries> Series,
    IReadOnlyList<BreakdownBandArea> Bands,
    bool Normalized,
    bool SeparateTypes,
    double? SuggestedYMin,
    double? SuggestedYMax,
    IReadOnlyList<string>? YCategoryLabels,
    string? LegendNote)
{
    public static readonly BreakdownResult Empty = new(
        BreakdownChartKind.Empty, Array.Empty<int>(), string.Empty, string.Empty,
        Array.Empty<BreakdownSeries>(), Array.Empty<BreakdownBandArea>(), false, false, null, null, null, null);
}

/// <summary>
///     Pure per-level aggregation for the By-Level Breakdown widget (docs
///     …/HomePageWidgets/by-level-breakdown.md). Turns a scope's worth of
///     <see cref="BreakdownRecord" />s into a render-ready <see cref="BreakdownResult" />.
///     No theming, no I/O — unit-tested in ScoreTracker.Tests.Components.
///
///     Populations (owner-confirmed): Distribution is over CLEARED charts; Completion is
///     over the WHOLE folder (unplayed = not met); Breakdown's remainder is optional.
/// </summary>
public static class ByLevelAggregator
{
    private sealed record Draw(ChartType? Type, string Label, char Suffix, bool Dashed);

    public static BreakdownResult Aggregate(
        ByLevelBreakdownConfig config,
        IReadOnlyList<BreakdownRecord> records,
        BreakdownScales scales)
    {
        var buckets = Buckets(config);
        if (buckets.Count == 0 || records.Count == 0) return BreakdownResult.Empty;

        var draws = Draws(config).ToArray();
        var separate = draws.Length > 1;
        var xTitle = config.Scope == BreakdownChartScope.CoOp ? "Players" : "Level";
        var legendNote = separate
            ? config.Aggregation == BreakdownAggregation.Breakdown
                ? "left bar = Singles · right = Doubles"
                : "solid = Singles · dashed = Doubles"
            : null;

        return config.Aggregation switch
        {
            BreakdownAggregation.Breakdown =>
                Breakdown(config, records, scales, buckets, draws, separate, xTitle, legendNote),
            BreakdownAggregation.Completion =>
                Completion(config, records, scales, buckets, draws, separate, xTitle, legendNote),
            _ => Distribution(config, records, scales, buckets, draws, separate, xTitle, legendNote)
        };
    }

    // ---- scope ----

    private static IReadOnlyList<int> Buckets(ByLevelBreakdownConfig config)
    {
        if (config.Scope == BreakdownChartScope.CoOp)
            return Span(config.MinPlayers, config.MaxPlayers);
        return Span(config.MinLevel, config.MaxLevel);
    }

    private static int[] Span(int a, int b)
    {
        var (lo, hi) = a <= b ? (a, b) : (b, a);
        return Enumerable.Range(lo, hi - lo + 1).ToArray();
    }

    private static IEnumerable<Draw> Draws(ByLevelBreakdownConfig config)
    {
        if (config.Scope == BreakdownChartScope.CoOp)
        {
            yield return new Draw(ChartType.CoOp, "Co-Op", 'C', false);
            yield break;
        }

        if (config.SeparateSinglesDoubles)
        {
            yield return new Draw(ChartType.Single, "Singles", 'S', false);
            yield return new Draw(ChartType.Double, "Doubles", 'D', true);
            yield break;
        }

        yield return new Draw(null, "S + D", ' ', false);
    }

    private static bool Matches(BreakdownRecord r, Draw draw) =>
        draw.Type == null
            ? r.Type is ChartType.Single or ChartType.Double
            : r.Type == draw.Type;

    private static IReadOnlyList<BreakdownRecord> Folder(
        IReadOnlyList<BreakdownRecord> records, Draw draw, int bucket) =>
        records.Where(r => r.Bucket == bucket && Matches(r, draw)).ToArray();

    // ---- Distribution ----

    private static readonly DistributionSeries[] OrdinalAllowed =
        { DistributionSeries.Min, DistributionSeries.Average, DistributionSeries.Max };

    private static BreakdownResult Distribution(
        ByLevelBreakdownConfig config, IReadOnlyList<BreakdownRecord> records, BreakdownScales scales,
        IReadOnlyList<int> buckets, Draw[] draws, bool separate, string xTitle, string? legendNote)
    {
        var ordinal = config.Metric is BreakdownMetric.LetterGrade or BreakdownMetric.Plate;
        if (config.Metric == BreakdownMetric.Pass) return BreakdownResult.Empty; // Pass has no distribution

        // Which stat keys, in a stable order.
        var keys = new List<(DistributionSeries? Named, int? Custom)>();
        foreach (var s in Enum.GetValues<DistributionSeries>())
            if (config.Series.Contains(s) && (!ordinal || OrdinalAllowed.Contains(s)))
                keys.Add((s, null));
        if (!ordinal)
            foreach (var p in config.CustomPercentiles.Where(p => p is >= 1 and <= 99).Distinct().OrderBy(p => p))
                keys.Add((null, p));
        if (keys.Count == 0) keys.Add((DistributionSeries.Average, null)); // never draw nothing

        // Per draw × bucket: the sorted cleared metric values.
        var sorted = new Dictionary<(int Draw, int Bucket), double[]>();
        for (var d = 0; d < draws.Length; d++)
            foreach (var bucket in buckets)
                sorted[(d, bucket)] = ClearedValues(Folder(records, draws[d], bucket), config.Metric)
                    .OrderBy(v => v).ToArray();

        var series = new List<BreakdownSeries>();
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        for (var d = 0; d < draws.Length; d++)
        {
            for (var k = 0; k < keys.Count; k++)
            {
                var (named, custom) = keys[k];
                var values = new double?[buckets.Count];
                for (var i = 0; i < buckets.Count; i++)
                {
                    var v = custom.HasValue
                        ? Percentile(sorted[(d, buckets[i])], custom.Value)
                        : StatValue(sorted[(d, buckets[i])], named!.Value);
                    values[i] = v;
                    if (v.HasValue) { min = Math.Min(min, v.Value); max = Math.Max(max, v.Value); }
                }

                var label = custom.HasValue ? $"P{custom.Value}" : StatLabel(named!.Value);
                series.Add(new BreakdownSeries(
                    separate ? $"{label} · {draws[d].Suffix}" : label,
                    SeriesColor.Qualitative(k), draws[d].Dashed, draws[d].Type, values));
            }
        }

        var bands = Bands(config, draws, buckets, sorted, ordinal, ref min, ref max);

        if (double.IsInfinity(min)) return BreakdownResult.Empty;

        double? yMin, yMax;
        IReadOnlyList<string>? yLabels = null;
        var yTitle = config.Metric.ToString();
        if (ordinal)
        {
            yLabels = config.Metric == BreakdownMetric.Plate ? scales.PlateNames : scales.GradeNames;
            yMin = Math.Max(0, min - 0.5);
            yMax = max + 0.5;
        }
        else
        {
            var pad = (max - min) * 0.12;
            if (pad < 1) pad = Math.Max(1, (max - min) * 0.05 + 1);
            yMin = Math.Max(0, min - pad);
            yMax = Math.Min(1_000_000, max + pad);
            yTitle = "Score";
        }

        return new BreakdownResult(BreakdownChartKind.Lines, buckets, xTitle, yTitle, series, bands,
            false, separate, yMin, yMax, yLabels, legendNote);
    }

    private static IEnumerable<double> ClearedValues(IReadOnlyList<BreakdownRecord> folder, BreakdownMetric metric) =>
        metric switch
        {
            BreakdownMetric.Score => folder.Where(r => r.IsPassed && r.Score.HasValue).Select(r => (double)r.Score!.Value),
            BreakdownMetric.LetterGrade => folder.Where(r => r.IsPassed && r.GradeRank.HasValue).Select(r => (double)r.GradeRank!.Value),
            BreakdownMetric.Plate => folder.Where(r => r.IsPassed && r.PlateRank.HasValue).Select(r => (double)r.PlateRank!.Value),
            _ => Enumerable.Empty<double>()
        };

    private static List<BreakdownBandArea> Bands(
        ByLevelBreakdownConfig config, Draw[] draws, IReadOnlyList<int> buckets,
        Dictionary<(int, int), double[]> sorted, bool ordinal, ref double min, ref double max)
    {
        var bands = new List<BreakdownBandArea>();
        if (ordinal || config.Metric != BreakdownMetric.Score || config.Band == BreakdownBand.None) return bands;

        for (var d = 0; d < draws.Length; d++)
        {
            var lower = new double?[buckets.Count];
            var upper = new double?[buckets.Count];
            for (var i = 0; i < buckets.Count; i++)
            {
                var s = sorted[(d, buckets[i])];
                var (lo, hi) = config.Band switch
                {
                    BreakdownBand.InterQuartile => (Percentile(s, 25), Percentile(s, 75)),
                    BreakdownBand.MinMax => (StatValue(s, DistributionSeries.Min), StatValue(s, DistributionSeries.Max)),
                    _ => (StatValue(s, DistributionSeries.MinusSigma), StatValue(s, DistributionSeries.PlusSigma))
                };
                lower[i] = lo;
                upper[i] = hi;
                if (lo.HasValue) min = Math.Min(min, lo.Value);
                if (hi.HasValue) max = Math.Max(max, hi.Value);
            }

            bands.Add(new BreakdownBandArea(SeriesColor.Qualitative(0), lower, upper));
        }

        return bands;
    }

    // ---- Breakdown (stacked bars) ----

    private static BreakdownResult Breakdown(
        ByLevelBreakdownConfig config, IReadOnlyList<BreakdownRecord> records, BreakdownScales scales,
        IReadOnlyList<int> buckets, Draw[] draws, bool separate, string xTitle, string? legendNote)
    {
        if (config.Metric is BreakdownMetric.Score) return BreakdownResult.Empty; // grades ARE the score bands

        var series = new List<BreakdownSeries>();

        if (config.Metric == BreakdownMetric.Pass)
        {
            AddSegment(series, "Unpassed", SeriesColor.Muted, draws, buckets, records, config.Normalize,
                (folder, total) => total - folder.Count(r => r.IsPassed), total => total);
            AddSegment(series, "Passed", SeriesColor.Pass, draws, buckets, records, config.Normalize,
                (folder, _) => folder.Count(r => r.IsPassed), total => total);
            return new BreakdownResult(BreakdownChartKind.StackedBars, buckets, xTitle, config.Normalize ? "% of folder" : "Charts",
                series, Array.Empty<BreakdownBandArea>(), config.Normalize, separate, null, null, null, legendNote);
        }

        var names = config.Metric == BreakdownMetric.Plate ? scales.PlateNames : scales.GradeNames;
        Func<BreakdownRecord, int?> rankOf = config.Metric == BreakdownMetric.Plate ? r => r.PlateRank : r => r.GradeRank;

        // Denominator = whole folder when the remainder is shown; else cleared-only.
        Func<int, IReadOnlyList<BreakdownRecord>, int> denom = config.IncludeUnplayed
            ? (total, _) => total
            : (_, folder) => folder.Count(r => r.IsPassed && rankOf(r).HasValue);

        if (config.IncludeUnplayed)
            AddSegment(series, "Not cleared", SeriesColor.Muted, draws, buckets, records, config.Normalize,
                (folder, total) => total - folder.Count(r => r.IsPassed && rankOf(r).HasValue),
                total => total);

        for (var rank = 0; rank < names.Count; rank++)
        {
            var capturedRank = rank;
            var frac = names.Count <= 1 ? 1.0 : rank / (double)(names.Count - 1);
            AddSegment(series, names[rank], SeriesColor.Rarity(frac), draws, buckets, records, config.Normalize,
                (folder, _) => folder.Count(r => r.IsPassed && rankOf(r) == capturedRank),
                total => 0, denom);
        }

        return new BreakdownResult(BreakdownChartKind.StackedBars, buckets, xTitle, config.Normalize ? "% of folder" : "Charts",
            series, Array.Empty<BreakdownBandArea>(), config.Normalize, separate, null, null, null, legendNote);
    }

    private static void AddSegment(
        List<BreakdownSeries> series, string label, SeriesColor color, Draw[] draws, IReadOnlyList<int> buckets,
        IReadOnlyList<BreakdownRecord> records, bool normalize,
        Func<IReadOnlyList<BreakdownRecord>, int, int> count, Func<int, int> denomFromTotal,
        Func<int, IReadOnlyList<BreakdownRecord>, int>? denom = null)
    {
        foreach (var draw in draws)
        {
            var values = new double?[buckets.Count];
            for (var i = 0; i < buckets.Count; i++)
            {
                var folder = Folder(records, draw, buckets[i]);
                var total = folder.Count;
                var raw = count(folder, total);
                if (!normalize) { values[i] = raw; continue; }
                var d = denom?.Invoke(total, folder) ?? denomFromTotal(total);
                values[i] = d > 0 ? 100.0 * raw / d : (double?)null;
            }

            series.Add(new BreakdownSeries(label, color, false, draw.Type, values));
        }
    }

    // ---- Completion (% of folder over thresholds) ----

    private static BreakdownResult Completion(
        ByLevelBreakdownConfig config, IReadOnlyList<BreakdownRecord> records, BreakdownScales scales,
        IReadOnlyList<int> buckets, Draw[] draws, bool separate, string xTitle, string? legendNote)
    {
        var thresholds = config.Metric == BreakdownMetric.Pass
            ? new List<CompletionThreshold> { new() { Kind = ThresholdKind.Pass } }
            : config.Thresholds;
        if (thresholds.Count == 0) thresholds = new List<CompletionThreshold> { DefaultThreshold(config.Metric) };

        var series = new List<BreakdownSeries>();
        for (var t = 0; t < thresholds.Count; t++)
        {
            var predicate = MeetsPredicate(thresholds[t], scales);
            var label = ThresholdLabel(thresholds[t]);
            foreach (var draw in draws)
            {
                var values = new double?[buckets.Count];
                for (var i = 0; i < buckets.Count; i++)
                {
                    var folder = Folder(records, draw, buckets[i]);
                    values[i] = folder.Count == 0 ? null : 100.0 * folder.Count(predicate) / folder.Count;
                }

                series.Add(new BreakdownSeries(
                    separate ? $"{label} · {draw.Suffix}" : label,
                    config.Metric == BreakdownMetric.Pass ? SeriesColor.Pass : SeriesColor.Qualitative(t),
                    draw.Dashed, draw.Type, values));
            }
        }

        return new BreakdownResult(BreakdownChartKind.Lines, buckets, xTitle, "% of folder", series,
            Array.Empty<BreakdownBandArea>(), false, separate, 0, 100, null, legendNote);
    }

    private static CompletionThreshold DefaultThreshold(BreakdownMetric metric) => metric switch
    {
        BreakdownMetric.Score => new CompletionThreshold { Kind = ThresholdKind.Score, Value = "990000" },
        BreakdownMetric.Plate => new CompletionThreshold { Kind = ThresholdKind.Plate, Value = "PG" },
        BreakdownMetric.LetterGrade => new CompletionThreshold { Kind = ThresholdKind.Grade, Value = "SSS" },
        _ => new CompletionThreshold { Kind = ThresholdKind.Pass }
    };

    private static Func<BreakdownRecord, bool> MeetsPredicate(CompletionThreshold threshold, BreakdownScales scales)
    {
        switch (threshold.Kind)
        {
            case ThresholdKind.Pass:
                return r => r.IsPassed;
            case ThresholdKind.Score:
                var min = int.TryParse(threshold.Value, out var s) ? s : 0;
                return r => r.IsPassed && r.Score >= min;
            case ThresholdKind.Grade:
                var gRank = IndexOf(scales.GradeNames, threshold.Value);
                return r => r.IsPassed && r.GradeRank >= gRank;
            case ThresholdKind.Plate:
                var pRank = IndexOf(scales.PlateNames, threshold.Value);
                return r => r.IsPassed && r.PlateRank >= pRank;
            default:
                return _ => false;
        }
    }

    private static int IndexOf(IReadOnlyList<string> names, string? value)
    {
        for (var i = 0; i < names.Count; i++)
            if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        return int.MaxValue; // an unknown threshold matches nothing
    }

    private static string ThresholdLabel(CompletionThreshold t) => t.Kind switch
    {
        ThresholdKind.Pass => "Passed",
        ThresholdKind.Score => int.TryParse(t.Value, out var v) ? $"≥ {v:N0}" : "≥ ?",
        _ => $"≥ {t.Value}"
    };

    // ---- stat helpers ----

    private static double? StatValue(IReadOnlyList<double> sorted, DistributionSeries key)
    {
        if (sorted.Count == 0) return null;
        return key switch
        {
            DistributionSeries.Min => sorted[0],
            DistributionSeries.Max => sorted[^1],
            DistributionSeries.Average => sorted.Average(),
            DistributionSeries.Median => Percentile(sorted, 50),
            DistributionSeries.P10 => Percentile(sorted, 10),
            DistributionSeries.P25 => Percentile(sorted, 25),
            DistributionSeries.P75 => Percentile(sorted, 75),
            DistributionSeries.P90 => Percentile(sorted, 90),
            DistributionSeries.MinusSigma => sorted.Average() - StdDev(sorted),
            DistributionSeries.PlusSigma => sorted.Average() + StdDev(sorted),
            _ => null
        };
    }

    /// <summary>Linear-interpolated percentile over an ascending-sorted list (p in [0,100]).</summary>
    public static double? Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return null;
        if (sorted.Count == 1) return sorted[0];
        var idx = (sorted.Count - 1) * p / 100.0;
        var lo = (int)Math.Floor(idx);
        var hi = (int)Math.Ceiling(idx);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
    }

    /// <summary>Population standard deviation.</summary>
    public static double StdDev(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    private static string StatLabel(DistributionSeries key) => key switch
    {
        DistributionSeries.Min => "Min",
        DistributionSeries.Max => "Max",
        DistributionSeries.Average => "Avg",
        DistributionSeries.Median => "Median",
        DistributionSeries.MinusSigma => "−1σ",
        DistributionSeries.PlusSigma => "+1σ",
        _ => key.ToString()
    };
}
