namespace ScoreTracker.Catalog.Domain;

internal interface IExternalChartAliasRepository
{
    /// <summary>Upserts by (source, ExternalKey); rows absent from <paramref name="aliases" /> are left untouched.</summary>
    Task SaveAliases(string source, IEnumerable<ExternalChartAlias> aliases,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalChartAlias>> GetAliases(string source, CancellationToken cancellationToken = default);

    /// <summary>Admin resolution: binds the key to a chart and marks it Manual.</summary>
    Task ResolveAlias(string source, string externalKey, Guid chartId, DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);
}
