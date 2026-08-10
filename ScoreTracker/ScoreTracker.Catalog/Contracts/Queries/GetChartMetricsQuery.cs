using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every banked piucenter metric for every chart, raw — the passthrough behind the CSV
///     export's <c>pc:</c> columns (docs/design/charts-srp.md §8). Metric names are
///     piucenter's own vocabulary, so this is deliberately untyped where
///     <see cref="ChartStepAnalysisRecord" /> is shaped: that record curates four scalars and
///     the badge fractions for display, and cannot carry practice ranks, segment badges or
///     rare-pattern counts without becoming a second copy of the source's schema.
///     <para>Charts with no banked analysis are absent. Reads the repository's cache.</para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartMetricsQuery
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>>;

/// <summary>
///     Just the distinct metric names, for the export dialog's family counts. Separate from
///     <see cref="GetChartMetricsQuery" /> so opening the picker does not walk four thousand
///     charts' values to learn what nine hundred name strings are.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartMetricNamesQuery : IQuery<IReadOnlyList<string>>;
