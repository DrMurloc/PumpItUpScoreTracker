namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Places a stage break on a chart's timeline. A death at judgement count J happened at the
///     J-th judgement event — tap rows and hold ticks both judge — so its position is a lookup
///     into the chart's sorted judgement-event times, not a fraction of the clock: judgements
///     bunch inside runs and holds, and mapping "X% of judgements" to "X% of the strip"
///     misplaces real deaths by up to ten seconds (docs/design/step-chart-failure-map.md D10).
///     <para>
///         The file's implied event count and the game's judged total rarely agree exactly even
///         on authentic files (the game's totals are authored round numbers), so J is rescaled
///         through their ratio first — the ±2% pin gate upstream bounds how far that stretch can
///         reach (D9). Pure game model over primitives, same family as
///         <see cref="StageBreakCauseSolver" />: no ports, no doubles needed to test.
///     </para>
/// </summary>
public static class BreakPositionSolver
{
    /// <summary>
    ///     The second the run ended, or null when the inputs cannot say: no judgements, no
    ///     judged total to rescale against, or an empty timeline. A count past the judged total
    ///     clamps to the final event — the importer has never produced one, but a lie at the
    ///     edge would place a pin past the chart.
    /// </summary>
    public static decimal? Place(int judged, IReadOnlyList<decimal> eventTimes, int noteCount)
    {
        if (judged <= 0 || noteCount <= 0 || eventTimes.Count == 0) return null;

        var rescaled = (int)Math.Round(judged * (decimal)eventTimes.Count / noteCount,
            MidpointRounding.AwayFromZero);
        var index = Math.Clamp(rescaled, 1, eventTimes.Count);
        return eventTimes[index - 1];
    }

    /// <summary>
    ///     Groups placed deaths into pins BY NOTE: runs stack into one ×N pin only when they
    ///     placed on the same judgement event (docs/design/step-chart-failure-map.md D1, owner
    ///     amendment 2026-08-31). A time window is the wrong instrument here — the original
    ///     1.5 s chain-merge smeared distinct spikes into one smudge, and a second is a long
    ///     time in a rhythm game; the note grid itself absorbs the placement estimate, because
    ///     nearby judged counts rescale onto the same event. Returned in time order.
    /// </summary>
    public static IReadOnlyList<BreakPinCluster> Cluster(IEnumerable<decimal> times)
    {
        return times.GroupBy(t => t)
            .OrderBy(g => g.Key)
            .Select(g => new BreakPinCluster(g.Key, g.Count(), g.Key, g.Key))
            .ToArray();
    }
}

/// <summary>One pin on the failure rail: where, how many runs, and the span they cover.</summary>
[ExcludeFromCodeCoverage]
public readonly record struct BreakPinCluster(decimal Time, int Count, decimal From, decimal To);
