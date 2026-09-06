using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Which of these charts are rest charts, and by how much (D29). The mix decides the folders —
///     a chart's level is per-mix, and the whole rule is relative to the folder it sits in — so the
///     same chart can be a rest chart on one mix and not on another.
///     <para>
///         Charts with no step analysis are absent from the result rather than reported as failing:
///         "not measured" and "measured and not restful" are different answers.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRestChartFactsQuery(MixEnum Mix, IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyList<RestChartFacts>>;
