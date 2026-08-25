using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

internal interface IChartFolderBaselineRepository
{
    /// <summary>
    ///     Replaces every baseline row for a mix. A rebuild recomputes the mix whole — a
    ///     folder that lost its last analyzed chart has to lose its rows too, and a
    ///     row-by-row merge would leave them behind.
    /// </summary>
    Task ReplaceBaselines(MixEnum mix, IEnumerable<ChartFolderBaseline> baselines,
        CancellationToken cancellationToken = default);

    /// <summary>One folder's baselines, keyed by badge.</summary>
    Task<IReadOnlyDictionary<string, ChartFolderBaseline>> GetFolderBaselines(MixEnum mix, ChartType type, int level,
        CancellationToken cancellationToken = default);
}
