using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Decides which half of a shared video each of one song's charts occupies
///     (docs/design/video-sides.md). Only a URL held by exactly two singles-family charts of
///     the one song is a two-sided video — the total-row count guards against cross-song
///     mislinks, which must stay sideless. Same-type pairs order by level (the lower level
///     plays on the left); a Single + Performance pair can't be ordered by level, so its
///     hand-researched sides are preserved rather than recomputed.
/// </summary>
internal static class VideoSideAssigner
{
    /// <summary>
    ///     Sides for one song's video-carrying charts. A returned side sets the column, a
    ///     returned null clears it, and a chart absent from the result is left untouched —
    ///     the preserve case, for pairs whose sides can only come from watching the video.
    /// </summary>
    public static IReadOnlyDictionary<Guid, VideoSide?> ComputeSides(
        IReadOnlyCollection<VideoChart> songCharts,
        Func<string, int> totalChartsOnUrl)
    {
        var result = new Dictionary<Guid, VideoSide?>();
        foreach (var group in songCharts.GroupBy(c => c.VideoUrl, StringComparer.Ordinal))
        {
            var charts = group.ToArray();
            if (totalChartsOnUrl(group.Key) != charts.Length || charts.Length != 2 ||
                !charts.All(c => c.Type is ChartType.Single or ChartType.SinglePerformance))
            {
                foreach (var chart in charts) result[chart.ChartId] = null;
                continue;
            }

            // Pairable but not orderable: never guess a side, never wipe a hand-set one.
            if (charts[0].Type != charts[1].Type || charts[0].Level == charts[1].Level) continue;

            var (lower, higher) = charts[0].Level < charts[1].Level
                ? (charts[0], charts[1])
                : (charts[1], charts[0]);
            result[lower.ChartId] = VideoSide.Left;
            result[higher.ChartId] = VideoSide.Right;
        }

        return result;
    }
}

/// <summary>One chart's video linkage, as the assigner sees it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record VideoChart(Guid ChartId, ChartType Type, int Level, string VideoUrl);
