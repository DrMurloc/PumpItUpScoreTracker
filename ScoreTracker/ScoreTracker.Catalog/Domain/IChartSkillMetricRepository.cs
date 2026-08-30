namespace ScoreTracker.Catalog.Domain;

internal interface IChartSkillMetricRepository
{
    /// <summary>Replaces every metric row for (chartId, source) — a one-off, single-chart refresh.</summary>
    Task ReplaceChartMetrics(Guid chartId, string source, IEnumerable<ChartSkillMetric> metrics,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces every listed chart's rows for a source as ONE ingestion, evicting the
    ///     source's cache once at the end instead of once per chart. This is the only shape an
    ///     ingestion may write through (owner, 2026-08-30): the per-chart method evicts the
    ///     whole-source cache on every call, and a snapshot upload made ~4,500 of them — every
    ///     live read between two writes re-hydrated the full metric table just to have the next
    ///     write throw it away, which is what was taking prod down for the length of an upload.
    /// </summary>
    Task ReplaceChartMetrics(string source,
        IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>> metricsByChart,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChartSkillMetric>> GetMetrics(IEnumerable<Guid> chartIds, string source,
        CancellationToken cancellationToken = default);

    /// <summary>Every chart's metric rows for a source, keyed by chart — the search's bulk read.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>> GetMetricsByChart(string source,
        CancellationToken cancellationToken = default);

    /// <summary>The gap-driven crawl's "already have it" set.</summary>
    Task<IReadOnlySet<Guid>> GetChartIdsWithMetrics(string source, CancellationToken cancellationToken = default);
}
