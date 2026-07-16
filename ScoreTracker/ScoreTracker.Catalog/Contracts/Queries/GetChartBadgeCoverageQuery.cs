using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Raw banked badge coverage per chart — piucenter's own badge name → the fraction of
///     the chart's segments carrying it — for analytics that need the measured profile
///     rather than a display projection. <see cref="GetChartSkillChipsQuery" /> maps these
///     onto the coarse Skill vocabulary by taking a max per mapped skill and gating each
///     badge on a threshold; that is right for chips and lossy for comparison, because
///     three kinds of bracket collapse into one number and a chart whose every twist badge
///     sits under its bar reads as having no twists at all. Consumers here get the badges
///     as banked: no mapping, no max, no thresholds, no defaults. Whole-chart qualities
///     (bursty, sustained) never carry a coverage fraction and so never appear — they are
///     intensity facts, and their scalars ride <see cref="GetChartStepAnalysesQuery" />.
///     Charts with no banked metrics are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartBadgeCoverageQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>>>
{
}
