using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     SeedChartId/SeedPeerRanking carry a recommendation's provenance when a category
    ///     derives its picks from another chart (Hot Streak: "you crushed the seed, here is
    ///     more like it"); ranking is the 0–1 cohort percentile the seed cleared, null when
    ///     the bar was off. Categories without provenance leave both null.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ChartRecommendation(Name Category, Guid ChartId, string Explanation, string ChartDetails = "",
        Guid? SeedChartId = null, double? SeedPeerRanking = null)
    {
    }
}
