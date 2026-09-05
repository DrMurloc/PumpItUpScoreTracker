using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     The four-lever read model over a session's chart rows (docs/design/march-of-murlocs.md
///     §11.6) and the clock those rows sit on. Pure functions: everything here is derived from
///     the rows and the board's window, never stored, so the Season page's derived columns,
///     the Breakdown and the Compare views cannot disagree about the same session.
/// </summary>
internal static class MoMLeverMath
{
    /// <summary>
    ///     Total score is <i>how many charts × how hard × how well</i>, capped by time — so a
    ///     session is these four numbers. Downtime is the window less the songs, floored at
    ///     zero: a closing chart may overhang the window (§2.9), and a session that filled it
    ///     has no rest by construction. The grade is the grade of the average score on the
    ///     board's mix, which is what a player would read off their own scores.
    /// </summary>
    public static MoMLevers Levers(IReadOnlyList<MoMSessionChart> charts, TimeSpan window, MixEnum mix)
    {
        if (charts.Count == 0)
            return new MoMLevers(0, 0, 0, PhoenixScore.Min, PhoenixLetterGrade.F, window, TimeSpan.Zero, 0);
        var songTime = SongTime(charts);
        var downtime = songTime >= window ? TimeSpan.Zero : window - songTime;
        var averageScore = PhoenixScore.From((int)Math.Round(charts.Average(c => (double)(int)c.Score)));
        return new MoMLevers(
            charts.Count,
            charts.Average(c => c.BalancedLevel),
            charts.Average(c => (int)c.Chart.Level),
            averageScore,
            averageScore.LetterGradeFor(mix),
            downtime,
            songTime,
            charts.Sum(c => c.SessionScore));
    }

    public static TimeSpan SongTime(IReadOnlyList<MoMSessionChart> charts)
    {
        return TimeSpan.FromTicks(charts.Sum(c => c.Chart.Song.Duration.Ticks));
    }

    /// <summary>
    ///     Where each chart sits on the clock. An imported session carries every play's stamp,
    ///     so its charts start where they really started, relative to the first one. A
    ///     hand-entered session carries none: its rest is spread evenly between charts, which
    ///     is the only honest shape when nothing recorded where the breaks fell — the pace
    ///     chart labels it as derived. Either way the rows keep their stored order.
    /// </summary>
    public static IReadOnlyList<MoMTimedChart> Timeline(IReadOnlyList<MoMSessionChart> charts, TimeSpan window)
    {
        if (charts.Count == 0) return Array.Empty<MoMTimedChart>();
        var result = new List<MoMTimedChart>(charts.Count);
        if (charts.All(c => c.PlayedAt != null))
        {
            // A stamp is when the play was recorded — its end — so a chart starts a song's
            // length before it. The first chart's start is the session's zero.
            var origin = charts[0].PlayedAt!.Value - charts[0].Chart.Song.Duration;
            foreach (var chart in charts)
            {
                var startsAt = chart.PlayedAt!.Value - chart.Chart.Song.Duration - origin;
                result.Add(Timed(chart, startsAt < TimeSpan.Zero ? TimeSpan.Zero : startsAt));
            }

            return result;
        }

        var songTime = SongTime(charts);
        var downtime = songTime >= window ? TimeSpan.Zero : window - songTime;
        var gap = charts.Count > 1 ? downtime / (charts.Count - 1) : TimeSpan.Zero;
        var cursor = TimeSpan.Zero;
        foreach (var chart in charts)
        {
            result.Add(Timed(chart, cursor));
            cursor += chart.Chart.Song.Duration + gap;
        }

        return result;
    }

    private static MoMTimedChart Timed(MoMSessionChart chart, TimeSpan startsAt)
    {
        var length = chart.Chart.Song.Duration;
        var perSecond = length.TotalSeconds > 0 ? chart.SessionScore / length.TotalSeconds : 0;
        return new MoMTimedChart(chart, startsAt, length, perSecond);
    }
}
