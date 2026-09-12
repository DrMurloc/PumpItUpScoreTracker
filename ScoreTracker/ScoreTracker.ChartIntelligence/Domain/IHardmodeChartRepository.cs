using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>The Hardmode list's storage. Vertical-internal — outside callers read the Domain port.</summary>
internal interface IHardmodeChartRepository
{
    /// <summary>
    ///     Replaces the mix's whole list. Wholesale on purpose: a partial write would leave last
    ///     week's charts qualifying beside this week's, and nothing downstream could tell.
    /// </summary>
    Task Replace(MixEnum mix, IReadOnlyCollection<HardmodeChartRecord> charts, DateTimeOffset computedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HardmodeChartRecord>> Get(MixEnum mix, CancellationToken cancellationToken);
}
