namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Catalog-internal port for the banked step timelines. Replace is batch-only on purpose —
///     an ingestion writes thousands of charts and the cache must turn over once, the same
///     lesson the skill-metric repository already carries.
/// </summary>
internal interface IChartStepChartRepository
{
    Task Replace(IReadOnlyDictionary<Guid, BankedStepChart> banked, CancellationToken cancellationToken = default);

    Task<BankedStepChart?> Get(Guid chartId, CancellationToken cancellationToken = default);
}

/// <summary>The stored row: codec bytes plus the vintage they were enriched from.</summary>
internal sealed record BankedStepChart(string Vintage, DateTimeOffset UpdatedAt, byte[] Payload);
