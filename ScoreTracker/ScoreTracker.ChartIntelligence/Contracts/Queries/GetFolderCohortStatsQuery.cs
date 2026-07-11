using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    /// <summary>
    ///     How the player's folder pass count sits against similar players (round 7):
    ///     merges the folder's materialized pass histograms across the competitive-level
    ///     buckets inside the ±0.5 "similar players" window. Null when the folder has no
    ///     cohort data (CoOp, unpopulated mixes, or a first run before the daily rebuild).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetFolderCohortStatsQuery(MixEnum Mix, ChartType ChartType, int Level,
        double CompetitiveLevel, int PassCount) : IQuery<FolderCohortSummaryRecord?>;
}
