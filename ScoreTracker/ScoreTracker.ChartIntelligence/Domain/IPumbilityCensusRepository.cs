using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>One chart's standing in a cohort's pools.</summary>
internal sealed record PumbilityCensusRecord(Guid ChartId, int Appearances, TierListCategory Category, int Order);

/// <summary>
///     The materialized PUMBILITY census (docs/design/pumbility-tier-list.md §8). Reads are
///     always one folder for one cohort; writes always replace that folder wholesale, because
///     the nightly job recomputes it from scratch and a chart that fell out of every pool has
///     to stop being listed.
/// </summary>
internal interface IPumbilityCensusRepository
{
    Task SaveFolder(MixEnum mix, ChartType chartType, DifficultyLevel level, string cohortKey,
        IEnumerable<PumbilityCensusRecord> entries, CancellationToken cancellationToken);

    Task<IEnumerable<PumbilityCensusRecord>> GetFolder(MixEnum mix, ChartType chartType, DifficultyLevel level,
        string cohortKey, CancellationToken cancellationToken);

    /// <summary>
    ///     Which folders this cohort can speak for at all — the ones holding at least one chart
    ///     somebody pools. Drives the folder selector's disabled options and the redirect that
    ///     sends a direct URL to the nearest folder with an answer.
    /// </summary>
    Task<IEnumerable<(ChartType ChartType, int Level)>> GetFoldersWithData(MixEnum mix, string cohortKey,
        CancellationToken cancellationToken);
}
