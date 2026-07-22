using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     The canonical weekly-board reading order, carried over from Phoenix 1: hardest first.
///     Descending by level, and within a level <b>singles before doubles</b> (a single is read
///     first, the same-numbered double next). Co-ops sort last, and among them the group charts
///     (player counts 3/4/5) come ahead of the 2-player duet.
///     <para>
///         The single source both the board query (<c>GetWeeklyBoardQuery</c>) and the homepage
///         Weekly widget order by, so the two can never drift — an earlier "doubles first"
///         translation had them disagreeing with the Phoenix 1 order.
///     </para>
/// </summary>
public static class WeeklyBoardOrder
{
    /// <summary>
    ///     A sortable key for one chart. Order an <c>IEnumerable</c> of charts (or chart-bearing
    ///     rows) by this ascending and it lands in canonical board order. A null chart (catalog
    ///     miss) sorts to the very end rather than claiming level 0.
    /// </summary>
    public static (int Group, int Rank, int TypeRank, string Name) SortKey(Chart? chart)
    {
        if (chart == null) return (2, 0, 0, string.Empty);
        var name = chart.Song.Name.ToString();
        if (chart.Type == ChartType.CoOp)
            // Co-ops last; player counts 3/4/5 first, the 2-player duet last. int.MaxValue for the
            // 2P sinks it below 3/4/5 under an ascending sort.
            return (1, chart.PlayerCount == 2 ? int.MaxValue : chart.PlayerCount, 0, name);
        // Non-co-op: level descending (negated), then singles (0) before doubles (1) within a level.
        return (0, -(int)chart.Level, chart.Type == ChartType.Single ? 0 : 1, name);
    }
}
