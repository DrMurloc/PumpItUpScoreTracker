using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Decides the sides for ONE video URL's charts at a registration event — a chart being
///     created, or an admin pointing a chart at a video (docs/design/video-sides.md). Sides
///     are durable data: nothing re-derives a side an earlier event or a hand audit stored,
///     so a decision exists only for the group the event just formed. A URL is a two-sided
///     video only while exactly two singles-family charts of one song hold it — the total-row
///     count guards against cross-song mislinks, which must stay sideless. Same-type pairs
///     order by level (the lower level plays on the left); a Single + Performance pair can't
///     be ordered by level, so it stays unassigned until researched by hand.
/// </summary>
internal static class VideoSideAssigner
{
    /// <summary>
    ///     The sides for the URL's charts, keyed by chart id — empty when the group isn't a
    ///     derivable pair (solo, crowded, cross-song, mixed Single+Performance, or a level
    ///     tie), in which case the caller writes nothing at all.
    /// </summary>
    public static IReadOnlyDictionary<Guid, VideoSide> DecideSides(
        IReadOnlyCollection<VideoChart> songChartsOnUrl, int totalChartsOnUrl)
    {
        var none = new Dictionary<Guid, VideoSide>();
        if (songChartsOnUrl.Count != 2 || totalChartsOnUrl != 2) return none;
        var charts = songChartsOnUrl.ToArray();
        if (!charts.All(c => c.Type is ChartType.Single or ChartType.SinglePerformance)) return none;
        if (charts[0].Type != charts[1].Type || charts[0].Level == charts[1].Level) return none;

        var (lower, higher) = charts[0].Level < charts[1].Level
            ? (charts[0], charts[1])
            : (charts[1], charts[0]);
        return new Dictionary<Guid, VideoSide>
        {
            [lower.ChartId] = VideoSide.Left,
            [higher.ChartId] = VideoSide.Right
        };
    }
}

/// <summary>One chart's video linkage, as the assigner sees it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record VideoChart(Guid ChartId, ChartType Type, int Level);
