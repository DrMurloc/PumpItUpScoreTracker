namespace ScoreTracker.Catalog.Domain;

internal interface IChartSkillMetricRepository
{
    /// <summary>Replaces every metric row for (chartId, source) — a crawl is a full refresh per chart.</summary>
    Task ReplaceChartMetrics(Guid chartId, string source, IEnumerable<ChartSkillMetric> metrics,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChartSkillMetric>> GetMetrics(IEnumerable<Guid> chartIds, string source,
        CancellationToken cancellationToken = default);

    /// <summary>Every chart's metric rows for a source, keyed by chart — the search's bulk read.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>> GetMetricsByChart(string source,
        CancellationToken cancellationToken = default);

    /// <summary>The gap-driven crawl's "already have it" set.</summary>
    Task<IReadOnlySet<Guid>> GetChartIdsWithMetrics(string source, CancellationToken cancellationToken = default);
}
