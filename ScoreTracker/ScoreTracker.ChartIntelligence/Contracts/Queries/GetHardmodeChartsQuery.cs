using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The week's Hardmode chart list for one mix — every qualifying chart with why it qualified.
///     Empty until the census has run, which is what a mix without Hardmode looks like too
///     (docs/design/hardmode-leaderboard.md).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHardmodeChartsQuery(MixEnum Mix) : IQuery<IReadOnlyList<HardmodeChartRecord>>;
