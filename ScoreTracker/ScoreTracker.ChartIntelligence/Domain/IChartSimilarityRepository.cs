using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

internal interface IChartSimilarityRepository
{
    /// <summary>
    ///     Wholesale rebuild of one chart's outgoing edges — the nightly job recomputes the
    ///     full top-K, so partial updates would only preserve stale neighbors.
    /// </summary>
    Task ReplaceEdges(MixEnum mix, Guid chartId, IReadOnlyList<ChartSimilarityEdge> edges,
        DateTimeOffset computedAt, CancellationToken cancellationToken);

    /// <summary>Outgoing edges for one chart, best match first.</summary>
    Task<IReadOnlyList<ChartSimilarityEdge>> GetEdges(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken);
}
