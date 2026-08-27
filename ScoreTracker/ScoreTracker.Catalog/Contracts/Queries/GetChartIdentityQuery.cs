using ScoreTracker.SharedKernel.Messaging;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every chart's identity chips, read against the folder each chart sits in for the given
///     mix (docs/design/chart-identity.md §6). The mix matters: a chart's level moves between
///     catalogs, so the same steps are judged against different company.
///     <para>
///         Charts with no banked step analysis are absent from the result — an empty entry and
///         a missing one would say the same thing, and a caller that wants to render "no data"
///         can tell from the absence.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartIdentityQuery(IReadOnlyList<Guid> ChartIds, MixEnum Mix)
    : IQuery<IReadOnlyDictionary<Guid, ChartIdentityRecord>>;
