using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The granular badge chips a chart surface renders: dominant badges first (piucenter's
///     top-three pick, in its order), then any other badge whose coverage clears its own bar.
///     Same selection policy as <see cref="GetChartSkillChipsQuery" /> minus the rollup hop,
///     so a chart that reads as "Twists 40%" there reads as the twist it actually is here.
///     Charts with no banked metrics are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartBadgeChipsQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgeChipRecord>>>;
