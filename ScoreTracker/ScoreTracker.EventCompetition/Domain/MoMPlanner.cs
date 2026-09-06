namespace ScoreTracker.EventCompetition.Domain;

/// <summary>One chart of a record book, already priced at the chosen energy.</summary>
internal sealed record MoMPlanChart(Guid ChartId, int Level, TimeSpan Duration, int Points)
{
    /// <summary>Points per second: what the greedy orders on, and what the page prints beside a chart.</summary>
    public double PointsPerSecond => Duration <= TimeSpan.Zero ? 0 : Points / Duration.TotalSeconds;
}

/// <summary>A suggested set, in the order it would be played, and the chart it closes on.</summary>
internal sealed record MoMPlan(IReadOnlyList<Guid> Set, Guid? ClosingChartId);

/// <summary>
///     The Planner's solver (docs/design/march-of-murlocs.md §11.5): the Season's future tense.
///     <para>
///         The engine is <c>AutoBuildSessionHandler</c>'s and predates this — charts in descending
///         points per second, taken while the rest budget holds. Slice 4b adds the two controls the
///         page actually offers. A <b>push cap</b> drops everything above a level, because a plan
///         built from your best charts is not a plan you can hold for ninety minutes. And the plan
///         <b>ends on a closing move</b>: the last chart taken already overhangs the window, since a
///         chart only has to START inside it (§1), so that slot is spent on the biggest chart left
///         rather than on whatever the rate happened to pick.
///     </para>
///     <para>
///         The closing move is a swap, not an extra. The greedy already takes one chart past the
///         budget — that chart IS the overhang the rule allows — so appending another would spend
///         the allowance twice.
///     </para>
/// </summary>
internal static class MoMPlanner
{
    /// <summary>No cap at all: All out.</summary>
    public const int NoLevelCap = 99;

    public static MoMPlan Solve(IReadOnlyList<MoMPlanChart> pool, TimeSpan window, TimeSpan restPerChart,
        int levelCap = NoLevelCap)
    {
        var eligible = pool.Where(c => c.Points > 0 && c.Level <= levelCap && c.Duration > TimeSpan.Zero)
            .OrderByDescending(c => c.PointsPerSecond)
            .ThenByDescending(c => c.Points)
            .ToArray();
        if (eligible.Length == 0) return new MoMPlan(Array.Empty<Guid>(), null);

        var taken = new List<MoMPlanChart>();
        var spent = TimeSpan.Zero;
        foreach (var chart in eligible)
        {
            if (spent >= window) break;

            taken.Add(chart);
            spent += chart.Duration + restPerChart;
        }

        if (taken.Count == 0) return new MoMPlan(Array.Empty<Guid>(), null);

        // The closing slot: the biggest chart left, if it beats the one the rate put there.
        var held = taken.Select(c => c.ChartId).ToHashSet();
        var biggest = eligible.Where(c => !held.Contains(c.ChartId))
            .MaxBy(c => c.Points);
        if (biggest != null && biggest.Points > taken[^1].Points) taken[^1] = biggest;

        return new MoMPlan(taken.Select(c => c.ChartId).ToArray(), taken[^1].ChartId);
    }

    /// <summary>
    ///     What a set is worth and what it costs: the points, the song time, and the wall clock the
    ///     rest between charts adds. The last chart's rest is not counted — there is nothing after it.
    /// </summary>
    public static (int Points, TimeSpan SongTime, TimeSpan WallClock) Totals(
        IReadOnlyList<MoMPlanChart> set, TimeSpan restPerChart)
    {
        if (set.Count == 0) return (0, TimeSpan.Zero, TimeSpan.Zero);

        var song = TimeSpan.FromTicks(set.Sum(c => c.Duration.Ticks));
        return (set.Sum(c => c.Points), song, song + restPerChart * (set.Count - 1));
    }
}
