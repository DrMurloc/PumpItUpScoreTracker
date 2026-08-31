using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Marries the two halves of the hybrid (docs/design/step-chart-failure-map.md D4–D9): the
///     snapshot's arrows, limbs and per-hold tick tallies with the .ssc's beats and authored
///     tick checkpoints. The snapshot is the backbone — it is what renders — and the .ssc
///     annotates it, adopted only where the two provably describe the same chart:
///     <list type="bullet">
///         <item>
///             <b>Beats align</b> when the .ssc's judgement rows match the snapshot's row-for-row
///             in count and in time — piu-annotate computed the snapshot's seconds FROM these
///             files, so agreement is expected and disagreement means a parse gap, a vintage
///             drift or a wrong chart pick. Aligned rows carry beat + quantization (Timing mode);
///             everything else keeps seconds only (D6).
///         </item>
///         <item>
///             <b>Ticks adopt</b> only when the .ssc grid reproduces the snapshot's authored tick
///             sum exactly; otherwise each span's tally spreads evenly — the approximation the
///             workshop's placement measurement already validated.
///         </item>
///         <item>
///             <b>Verdicts compute per mix</b> from the §7 criteria (Tier A/B/C ⇒ Excluded) and
///             the ±2% pin gate (Full vs StepsOnly), because NoteCount is per-mix and the same
///             chart can earn pins on one mix and not the other (D8/D9).
///         </item>
///     </list>
/// </summary>
internal static class StepChartEnricher
{
    /// <summary>Row times must reproduce within this to count as the same chart.</summary>
    private const decimal AlignmentEpsilon = 0.02m;

    private const decimal PinGate = 0.02m;
    private const decimal TierBTickDemand = 0.05m;
    private const decimal TierCDisagreement = 0.5m;

    public static EnrichedStepChart Enrich(SnapshotStepData snapshot, StepChartData? ssc,
        IReadOnlyDictionary<MixEnum, int?> noteCounts)
    {
        var rows = BuildRows(snapshot.Taps);
        var panels = snapshot.StepsType?.Contains("single", StringComparison.OrdinalIgnoreCase) == true &&
                     rows.All(r => r.PanelMask < 1 << 5) &&
                     snapshot.Holds.All(h => h.Panel < 5)
            ? 5
            : 10;

        var aligned = ssc is { HasTimeline: true } && Aligns(rows, ssc.Rows);
        if (aligned) AttachBeats(rows, ssc!.Rows);

        var snapshotTickSum = snapshot.TickSpans.Sum(s => s.Count);
        var tickTimes = aligned && ssc!.Ticks.Count == snapshotTickSum
            ? ssc.Ticks.Select(t => t.Time).OrderBy(t => t).ToArray()
            : SpreadTicks(snapshot.TickSpans);

        var tapRowCount = rows.Count;
        var holdRowCount = snapshot.Holds.Select(h => h.Start).Distinct().Count();
        var verdicts = noteCounts.ToDictionary(
            kv => kv.Key,
            kv => Judge(kv.Value, tapRowCount, holdRowCount, snapshotTickSum));

        return new EnrichedStepChart(panels, aligned, rows, snapshot.Holds, tickTimes,
            snapshot.Segments, snapshot.RangesOfInterest, verdicts, tapRowCount, snapshotTickSum,
            snapshot.SscFilePath, snapshot.StepsType, snapshot.Meter);
    }

    /// <summary>
    ///     stepfile-precision §7's three tiers, then the pin gate — the doc's criteria as code,
    ///     one home (D8). No judged total to compare against is StepsOnly, not Excluded: absence
    ///     of evidence indicts nothing, it just cannot license pins.
    /// </summary>
    internal static StepChartVerdict Judge(int? noteCount, int tapRows, int holdRows, int tickSum)
    {
        var implied = tapRows + tickSum;
        if (noteCount is not > 0) return new StepChartVerdict(StepChartVisibility.StepsOnly, noteCount, implied);

        var judged = noteCount.Value;
        var derivedTicks = judged - tapRows;

        // Tier A — arithmetically impossible: more tap rows than the game judges, or fewer
        // derived ticks than the file's own hold rows demand.
        if (tapRows > judged || (holdRows > 0 && derivedTicks < holdRows))
            return new StepChartVerdict(StepChartVisibility.Excluded, noteCount, implied);
        // Tier B — a hold-less file against a game demanding real tick volume: the file is not
        // the shipped chart, taps included.
        if (holdRows == 0 && derivedTicks > TierBTickDemand * judged)
            return new StepChartVerdict(StepChartVisibility.Excluded, noteCount, implied);
        // Tier C — wildly disagreeing totals.
        if (Math.Abs(implied - judged) > TierCDisagreement * judged)
            return new StepChartVerdict(StepChartVisibility.Excluded, noteCount, implied);

        return new StepChartVerdict(
            Math.Abs(implied - judged) <= PinGate * judged
                ? StepChartVisibility.Full
                : StepChartVisibility.StepsOnly,
            noteCount, implied);
    }

    /// <summary>
    ///     Arrows grouped into judgement rows on their exact serialized times — a jump is one
    ///     row wearing two panels, and the left-foot arrows ride a second mask so Feet mode
    ///     costs nothing at render time.
    /// </summary>
    private static List<EnrichedRow> BuildRows(IReadOnlyList<SnapshotArrow> taps)
    {
        return taps
            .GroupBy(t => t.Time)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var mask = 0;
                var left = 0;
                foreach (var arrow in g)
                {
                    mask |= 1 << arrow.Panel;
                    if (arrow.Limb == "l") left |= 1 << arrow.Panel;
                }

                return new EnrichedRow(g.Key) { PanelMask = mask, LeftMask = left };
            })
            .ToList();
    }

    /// <summary>Aligned rows adopt the ssc's beats and the quantization they imply.</summary>
    internal static void AttachBeats(IReadOnlyList<EnrichedRow> rows, IReadOnlyList<StepRow> sscRows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Beat = sscRows[i].Beat;
            rows[i].Quant = QuantOf(sscRows[i].Beat);
        }
    }

    internal static bool Aligns(IReadOnlyList<EnrichedRow> snapshotRows, IReadOnlyList<StepRow> sscRows)
    {
        if (snapshotRows.Count != sscRows.Count || snapshotRows.Count == 0) return false;
        for (var i = 0; i < snapshotRows.Count; i++)
            if (Math.Abs(snapshotRows[i].Time - sscRows[i].Time) > AlignmentEpsilon)
                return false;
        return true;
    }

    /// <summary>4ths through 48ths off the beat's denominator; 0 = off every modeled grid.</summary>
    internal static int QuantOf(decimal beat)
    {
        Span<int> divisors = stackalloc int[] { 1, 2, 3, 4, 6, 8, 12 };
        foreach (var divisor in divisors)
        {
            var scaled = beat * divisor;
            if (Math.Abs(scaled - Math.Round(scaled)) < 0.001m) return 4 * divisor;
        }

        return 0;
    }

    private static decimal[] SpreadTicks(IReadOnlyList<SnapshotTickSpan> spans)
    {
        var times = new List<decimal>();
        foreach (var span in spans)
        {
            if (span.Count <= 0) continue;
            if (span.Count == 1)
            {
                times.Add((span.Start + span.End) / 2);
                continue;
            }

            var step = (span.End - span.Start) / (span.Count - 1);
            for (var k = 0; k < span.Count; k++) times.Add(span.Start + k * step);
        }

        times.Sort();
        return times.ToArray();
    }
}

// --- the snapshot side, as the ingest hands it over ---

internal sealed record SnapshotStepData(
    IReadOnlyList<SnapshotArrow> Taps,
    IReadOnlyList<SnapshotHold> Holds,
    IReadOnlyList<SnapshotTickSpan> TickSpans,
    IReadOnlyList<SnapshotSegment> Segments,
    IReadOnlyList<SnapshotRange> RangesOfInterest,
    string? SscFilePath,
    string? StepsType,
    int? Meter);

internal sealed record SnapshotArrow(int Panel, decimal Time, string Limb);

internal sealed record SnapshotHold(int Panel, decimal Start, decimal End, string Limb);

internal sealed record SnapshotTickSpan(decimal Start, decimal End, int Count);

internal sealed record SnapshotSegment(
    decimal Start,
    decimal End,
    decimal? Enps,
    IReadOnlyList<string>? Badges = null,
    decimal? Level = null,
    string? Pace = null);

internal sealed record SnapshotRange(decimal Start, decimal End);

// --- the banked result ---

internal sealed record EnrichedStepChart(
    int Panels,
    bool BeatsAligned,
    IReadOnlyList<EnrichedRow> Rows,
    IReadOnlyList<SnapshotHold> Holds,
    IReadOnlyList<decimal> TickTimes,
    IReadOnlyList<SnapshotSegment> Segments,
    IReadOnlyList<SnapshotRange> RangesOfInterest,
    IReadOnlyDictionary<MixEnum, StepChartVerdict> Verdicts,
    int TapRowCount,
    int TickSum,
    string? SscFile = null,
    string? StepsType = null,
    int? Meter = null);

internal sealed class EnrichedRow
{
    public EnrichedRow(decimal time)
    {
        Time = time;
    }

    public decimal Time { get; }
    public int PanelMask { get; set; }
    public int LeftMask { get; set; }
    public decimal? Beat { get; set; }
    public int Quant { get; set; }
}

internal sealed record StepChartVerdict(StepChartVisibility Visibility, int? NoteCount, int ImpliedTotal);
