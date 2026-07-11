namespace ScoreTracker.ChartIntelligence.Contracts
{
    /// <summary>
    ///     Result DTO for GetFolderCohortStatsQuery. PassPercentile follows the
    ///     ScoreRankingRecord convention: the fraction of the cohort at or below the
    ///     player's pass count, 1.0 = first place. (Lives at the Contracts root — the
    ///     Queries folder is reserved for IQuery types by the taxonomy ratchet.)
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record FolderCohortSummaryRecord(int PlayerCount, double AveragePasses, double PassPercentile);
}
