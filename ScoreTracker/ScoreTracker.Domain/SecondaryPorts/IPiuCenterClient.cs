using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Read port over piucenter.com's published step-analysis data (design doc
    ///     tier-lists-overhaul §8a). Implementations own version discovery and the
    ///     missing-file semantics of their static host.
    /// </summary>
    public interface IPiuCenterClient
    {
        /// <summary>Their full chart enumeration — every chart they have analysis for, all packs.</summary>
        Task<IReadOnlyList<PiuCenterChartListing>> GetChartTable(CancellationToken cancellationToken = default);

        /// <summary>One chart's skill analysis, or null when piucenter has no page for the key.</summary>
        Task<PiuCenterChartPage?> GetChartPage(string externalKey, CancellationToken cancellationToken = default);

        /// <summary>The per-skill, per-level ranked practice lists.</summary>
        Task<IReadOnlyList<PiuCenterPracticeEntry>> GetPracticeLists(CancellationToken cancellationToken = default);

        /// <summary>Their numeric difficulty prediction per chart key, flattened across folders.</summary>
        Task<IReadOnlyDictionary<string, decimal>> GetDifficultyPredictions(
            CancellationToken cancellationToken = default);
    }
}
