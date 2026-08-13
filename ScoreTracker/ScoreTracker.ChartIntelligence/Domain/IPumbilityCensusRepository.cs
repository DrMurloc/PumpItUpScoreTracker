using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>One chart's standing in a cohort's pools.</summary>
internal sealed record PumbilityCensusRecord(Guid ChartId, int Appearances, TierListCategory Category, int Order);

/// <summary>
///     The materialized PUMBILITY census (docs/design/pumbility-tier-list.md §8). Reads are
///     always one folder for one cohort; writes replace a folder across every cohort at once,
///     because the nightly job recomputes it from scratch and a chart that fell out of every
///     pool has to stop being listed.
/// </summary>
internal interface IPumbilityCensusRepository
{
    /// <summary>
    ///     Replaces every cohort's rows for one folder. Cohorts absent from
    ///     <paramref name="byCohort" /> end up with no rows, which is how a cohort says it
    ///     cannot speak for this folder — writing a full set of zeros for every cohort that
    ///     cannot reach a folder would be most of the table, since a cohort's pools only
    ///     cover a three-to-four level band.
    /// </summary>
    Task SaveFolder(MixEnum mix, ChartType chartType, DifficultyLevel level,
        IReadOnlyDictionary<string, IReadOnlyList<PumbilityCensusRecord>> byCohort,
        CancellationToken cancellationToken);

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
