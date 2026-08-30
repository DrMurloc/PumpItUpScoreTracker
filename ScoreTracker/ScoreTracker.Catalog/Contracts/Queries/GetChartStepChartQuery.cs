using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The banked step timeline for one chart as one mix sees it, or null — null when nothing
///     was ever banked, when the payload cannot be read, when the mix carries no verdict
///     (legacy mixes never do), and when the verdict is Excluded: absence is how a step file
///     that is provably not the shipped chart stays off the page
///     (docs/design/step-chart-failure-map.md D8).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartStepChartQuery(Guid ChartId, MixEnum Mix) : IQuery<ChartStepChartRecord?>;
