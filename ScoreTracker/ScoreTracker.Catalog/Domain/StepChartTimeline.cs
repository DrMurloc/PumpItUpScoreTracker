using System.Globalization;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Turns one selected .ssc chart into its judgement timeline: note rows and holds with
///     beats AND seconds (OFFSET/BPMS/STOPS/DELAYS/WARPS, chart-level tags overriding the
///     song's), and hold-tick times from the authored TICKCOUNTS grid. This is the half of the
///     hybrid the snapshot cannot supply — the snapshot serialized only seconds
///     (docs/design/step-chart-failure-map.md D4/D6).
///     <para>
///         Semantics follow StepMania 5: a measure is always four beats; a STOP pauses after
///         its beat, a DELAY before it; a WARP's span takes zero time and the rows inside it
///         are skipped, exactly as the game fakes them. A file using an encoding this walk does
///         not model (a non-positive BPM, a negative stop) yields
///         <see cref="StepChartData.HasTimeline" /> = false rather than wrong seconds — the
///         alignment tripwire downstream then keeps that chart on snapshot times.
///     </para>
/// </summary>
internal static class StepChartTimeline
{
    public static StepChartData? Build(StepFileDocument document, StepChartBlock chart)
    {
        var notes = chart.Tags.Get("NOTES");
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var panels = PanelLayout(chart.StepsType);
        if (panels == null) return null;

        var offset = ParseDecimal(Tag(document, chart, "OFFSET")) ?? 0m;
        var bpms = ParseBeatMap(Tag(document, chart, "BPMS"));
        var stops = ParseBeatMap(Tag(document, chart, "STOPS"));
        var delays = ParseBeatMap(Tag(document, chart, "DELAYS"));
        var warps = ParseBeatMap(Tag(document, chart, "WARPS"));
        var tickCounts = ParseBeatMap(Tag(document, chart, "TICKCOUNTS"));

        var supported = bpms.Count > 0 && bpms.All(b => b.Value > 0) &&
                        stops.All(s => s.Value >= 0) && delays.All(d => d.Value >= 0) &&
                        warps.All(w => w.Value >= 0);

        var (rows, holds) = ReadNotes(notes, panels.Value.Columns, panels.Value.Offset);
        if (supported)
        {
            rows = rows.Where(r => !IsWarped(r.Beat, warps)).ToList();
            holds = holds.Where(h => !IsWarped(h.StartBeat, warps)).ToList();
        }

        var tickBeats = supported ? TickBeats(holds, tickCounts) : new List<(decimal, int)>();

        if (!supported)
            return new StepChartData(panels.Value.Total, false, rows, holds,
                Array.Empty<StepTick>());

        var map = new TempoMap(offset, bpms, stops, delays, warps);
        foreach (var row in rows) row.Time = map.TimeAt(row.Beat);
        foreach (var hold in holds)
        {
            hold.StartTime = map.TimeAt(hold.StartBeat);
            hold.EndTime = map.TimeAt(hold.EndBeat);
        }

        var ticks = tickBeats
            .Select(t => new StepTick(t.Item1, map.TimeAt(t.Item1), t.Item2))
            .ToArray();

        return new StepChartData(panels.Value.Total, true, rows, holds, ticks);
    }

    private static string? Tag(StepFileDocument document, StepChartBlock chart, string name)
    {
        return chart.Tags.Get(name) ?? document.SongTags.Get(name);
    }

    /// <summary>
    ///     Columns in the file and where they sit on the pad. Half-double is authored six wide
    ///     but IS the middle of a doubles pad, so its columns land on panels 2–7 — the same
    ///     widening the analysis pipeline applies.
    /// </summary>
    private static (int Columns, int Offset, int Total)? PanelLayout(string stepsType)
    {
        return stepsType.ToLowerInvariant() switch
        {
            "pump-single" => (5, 0, 5),
            "pump-double" or "pump-couple" or "pump-routine" => (10, 0, 10),
            "pump-halfdouble" => (6, 2, 10),
            _ => null
        };
    }

    private static (List<StepRow> Rows, List<StepHold> Holds) ReadNotes(string notes, int columns, int offset)
    {
        var rows = new List<StepRow>();
        var holds = new List<StepHold>();
        var open = new StepHold?[columns];

        var measures = notes.Split(',');
        for (var m = 0; m < measures.Length; m++)
        {
            var lines = measures[m]
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();
            if (lines.Length == 0) continue;

            for (var r = 0; r < lines.Length; r++)
            {
                var beat = m * 4m + r * 4m / lines.Length;
                var line = lines[r];
                var mask = 0;
                for (var c = 0; c < columns && c < line.Length; c++)
                {
                    var panel = c + offset;
                    switch (line[c])
                    {
                        case '1':
                            mask |= 1 << panel;
                            break;
                        case '2':
                        case '4': // a roll head starts a hold body all the same
                            mask |= 1 << panel;
                            var hold = new StepHold(panel, beat);
                            open[c] = hold;
                            holds.Add(hold);
                            break;
                        case '3':
                            if (open[c] is { } opened)
                            {
                                opened.EndBeat = beat;
                                open[c] = null;
                            }

                            break;
                    }
                }

                if (mask != 0) rows.Add(new StepRow(beat, mask));
            }
        }

        // A head whose tail never came closes at its own beat — a zero-length body draws as a
        // note and never distorts the tick grid.
        foreach (var hold in holds)
            if (hold.EndBeat < hold.StartBeat)
                hold.EndBeat = hold.StartBeat;

        return (rows, holds);
    }

    /// <summary>
    ///     Checkpoint beats per hold: at a tick count of N, checkpoints sit on the 1/N grid,
    ///     inclusive of both edges when they land on it — the authored behaviour the snapshot's
    ///     per-hold tick tallies were derived from, which is what lets the two be reconciled.
    /// </summary>
    private static List<(decimal Beat, int Panel)> TickBeats(IReadOnlyList<StepHold> holds,
        IReadOnlyList<KeyValuePair<decimal, decimal>> tickCounts)
    {
        var ticks = new List<(decimal, int)>();
        foreach (var hold in holds)
        {
            var beat = hold.StartBeat;
            while (beat <= hold.EndBeat)
            {
                var count = CountAt(tickCounts, beat);
                if (count <= 0) break;
                var step = 1m / count;
                // Snap forward onto the grid, then walk it to the tail.
                var gridded = Math.Ceiling(beat / step) * step;
                if (gridded > hold.EndBeat) break;
                ticks.Add((gridded, hold.Panel));
                beat = gridded + step;
            }
        }

        return ticks;
    }

    private static int CountAt(IReadOnlyList<KeyValuePair<decimal, decimal>> tickCounts, decimal beat)
    {
        var count = 0m;
        foreach (var entry in tickCounts)
        {
            if (entry.Key > beat) break;
            count = entry.Value;
        }

        return (int)count;
    }

    private static bool IsWarped(decimal beat, IReadOnlyList<KeyValuePair<decimal, decimal>> warps)
    {
        return warps.Any(w => beat >= w.Key && beat < w.Key + w.Value);
    }

    /// <summary>"beat=value,beat=value" pairs, sorted by beat.</summary>
    private static IReadOnlyList<KeyValuePair<decimal, decimal>> ParseBeatMap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<KeyValuePair<decimal, decimal>>();
        var pairs = new List<KeyValuePair<decimal, decimal>>();
        foreach (var part in value.Split(','))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            if (decimal.TryParse(part[..eq].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture,
                    out var beat) &&
                decimal.TryParse(part[(eq + 1)..].Trim(), NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var number))
                pairs.Add(new KeyValuePair<decimal, decimal>(beat, number));
        }

        return pairs.OrderBy(p => p.Key).ToArray();
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    ///     Beat → seconds under the full gimmick vocabulary. Beat 0 sits at −OFFSET; queries are
    ///     answered by walking the merged boundary list, caching nothing — charts are built once
    ///     at ingest, never per request.
    /// </summary>
    private sealed class TempoMap
    {
        private readonly IReadOnlyList<KeyValuePair<decimal, decimal>> _bpms;
        private readonly IReadOnlyList<KeyValuePair<decimal, decimal>> _delays;
        private readonly decimal _offset;
        private readonly IReadOnlyList<KeyValuePair<decimal, decimal>> _stops;
        private readonly IReadOnlyList<KeyValuePair<decimal, decimal>> _warps;

        public TempoMap(decimal offset,
            IReadOnlyList<KeyValuePair<decimal, decimal>> bpms,
            IReadOnlyList<KeyValuePair<decimal, decimal>> stops,
            IReadOnlyList<KeyValuePair<decimal, decimal>> delays,
            IReadOnlyList<KeyValuePair<decimal, decimal>> warps)
        {
            _offset = offset;
            _bpms = bpms;
            _stops = stops;
            _delays = delays;
            _warps = warps;
        }

        public decimal TimeAt(decimal beat)
        {
            var time = -_offset;

            // BPM segments up to the queried beat, with warped spans contributing zero.
            for (var i = 0; i < _bpms.Count; i++)
            {
                var from = _bpms[i].Key;
                if (from >= beat) break;
                var to = i + 1 < _bpms.Count ? Math.Min(_bpms[i + 1].Key, beat) : beat;
                var span = to - from;
                span -= WarpedWithin(from, to);
                if (span > 0) time += span * 60m / _bpms[i].Value;
            }

            foreach (var stop in _stops)
                if (stop.Key < beat && !IsInsideWarp(stop.Key))
                    time += stop.Value;
            foreach (var delay in _delays)
                if (delay.Key <= beat && !IsInsideWarp(delay.Key))
                    time += delay.Value;

            return Math.Round(time, 6);
        }

        private decimal WarpedWithin(decimal from, decimal to)
        {
            var warped = 0m;
            foreach (var warp in _warps)
            {
                var start = Math.Max(warp.Key, from);
                var end = Math.Min(warp.Key + warp.Value, to);
                if (end > start) warped += end - start;
            }

            return warped;
        }

        private bool IsInsideWarp(decimal beat)
        {
            return _warps.Any(w => beat >= w.Key && beat < w.Key + w.Value);
        }
    }
}

/// <summary>One selected chart, timed. Beats always; seconds only when the timeline held.</summary>
internal sealed record StepChartData(
    int Panels,
    bool HasTimeline,
    IReadOnlyList<StepRow> Rows,
    IReadOnlyList<StepHold> Holds,
    IReadOnlyList<StepTick> Ticks);

/// <summary>A judgement row: every panel struck at one beat, taps and hold heads alike.</summary>
internal sealed class StepRow
{
    public StepRow(decimal beat, int panelMask)
    {
        Beat = beat;
        PanelMask = panelMask;
    }

    public decimal Beat { get; }
    public int PanelMask { get; }
    public decimal Time { get; set; }
}

internal sealed class StepHold
{
    public StepHold(int panel, decimal startBeat)
    {
        Panel = panel;
        StartBeat = startBeat;
        EndBeat = -1;
    }

    public int Panel { get; }
    public decimal StartBeat { get; }
    public decimal EndBeat { get; set; }
    public decimal StartTime { get; set; }
    public decimal EndTime { get; set; }
}

internal sealed record StepTick(decimal Beat, decimal Time, int Panel);
