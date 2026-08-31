using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Re-runs the .ssc-dependent half of enrichment from a banked payload and the archived
///     step file — the reprocess button's transform (docs/design/step-chart-failure-map.md D7).
///     The snapshot half never re-runs here (its rows ARE the payload); what refreshes is the
///     alignment, the beats, the adopted ticks, and the verdicts against the CURRENT per-mix
///     judged counts — which is the whole point: note counts refill from play and repaired
///     files land in the archive, and neither should demand a re-upload to take effect.
/// </summary>
internal static class StepChartReprocessor
{
    public static EnrichedStepChart Refresh(StepChartPayload payload, StepChartData? ssc,
        IReadOnlyDictionary<MixEnum, int?> noteCounts)
    {
        var rows = payload.Rows
            .Select(r => new EnrichedRow(r.T) { PanelMask = r.M, LeftMask = r.L })
            .ToList();

        var aligned = ssc is { HasTimeline: true } && StepChartEnricher.Aligns(rows, ssc.Rows);
        if (aligned) StepChartEnricher.AttachBeats(rows, ssc!.Rows);

        var tickTimes = aligned && ssc!.Ticks.Count == payload.TickSum
            ? ssc.Ticks.Select(t => t.Time).OrderBy(t => t).ToArray()
            : payload.Ticks;

        var holds = payload.Holds
            .Select(h => new SnapshotHold(h.P, h.S, h.E, h.L ? "l" : "r"))
            .ToArray();
        var holdRowCount = payload.Holds.Select(h => h.S).Distinct().Count();
        var verdicts = noteCounts.ToDictionary(
            kv => kv.Key,
            kv => StepChartEnricher.Judge(kv.Value, payload.TapRows, holdRowCount, payload.TickSum));

        return new EnrichedStepChart(payload.Panels, aligned, rows, holds, tickTimes,
            payload.Segments.Select(s => new SnapshotSegment(s.S, s.E, s.N, s.B, s.L, s.P)).ToArray(),
            payload.Ranges.Select(r => new SnapshotRange(r.S, r.E)).ToArray(),
            verdicts, payload.TapRows, payload.TickSum,
            payload.Ssc, payload.StepsType, payload.Meter);
    }
}
