using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The granular badge chips a chart surface renders: dominant badges first (piucenter's
///     top-three pick, in its order), then any other badge whose coverage clears its own bar.
///     No rollup hop: a chart the retired vocabulary would have called "Twists 40%" reads
///     here as the twist it actually is.
///     Charts with no banked metrics are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartBadgeChipsQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgeChipRecord>>>;
