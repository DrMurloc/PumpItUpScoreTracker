namespace ScoreTracker.Catalog.Domain;

internal enum ExternalAliasStatus
{
    Auto,
    Manual,
    NotFound
}

/// <summary>
///     One row of the generic external-name map (design doc §8a): an external source's
///     chart key, resolved (or not) to one of our charts. For piucenter the key doubles
///     as the fetch URL segment, so the alias set is also the crawl plan; NotFound rows
///     are the negative cache that keeps weekly runs near no-ops.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ExternalChartAlias(
    string ExternalKey,
    Guid? ChartId,
    ExternalAliasStatus Status,
    DateTimeOffset LastCheckedAt);
