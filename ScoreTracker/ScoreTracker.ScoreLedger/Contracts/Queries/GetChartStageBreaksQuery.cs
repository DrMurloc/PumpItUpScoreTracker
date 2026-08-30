using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Every judged stage break ever imported for one chart in one mix, anonymized — the
///     failure rail's data (docs/design/step-chart-failure-map.md D2/D3). Each row is a
///     judgement count (the position solver's input), the proven non-lifebar flag, and whether
///     the row is the asking viewer's own; no identity ever leaves the vertical. Private
///     players are counted — a count at a position is not a look at a player (D3).
///     <para>
///         Mix is explicit and mandatory: chart ids are cross-mix, and an unfiltered read
///         would pin one mix's deaths on the other's timeline.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartStageBreaksQuery(Guid ChartId, MixEnum Mix, Guid? ViewerId = null)
    : IQuery<IEnumerable<ChartStageBreakRecord>>;
