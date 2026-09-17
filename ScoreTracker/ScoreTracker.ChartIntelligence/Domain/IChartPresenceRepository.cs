using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>A mix's census columns and when the census that wrote them ran.</summary>
internal sealed record ChartPresenceColumns(IReadOnlyList<ChartPresenceColumnRow> Rows, DateTimeOffset? ComputedAt);

/// <summary>The PUMBILITY presence census's storage (docs/design/chart-presence-graph.md §7).</summary>
internal interface IChartPresenceRepository
{
    /// <summary>
    ///     Replaces the mix's whole census. Wholesale on purpose: a title's players and every chart's
    ///     rows on it are counted over one population, and a partial write would mix two.
    /// </summary>
    Task Replace(MixEnum mix, ChartPresenceCensusResult census, DateTimeOffset computedAt,
        CancellationToken cancellationToken);

    Task<ChartPresenceColumns> GetColumns(MixEnum mix, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChartPresenceRow>> GetRows(MixEnum mix, Guid chartId, CancellationToken cancellationToken);
}
