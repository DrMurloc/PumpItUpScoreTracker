using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Every level's personal-best population in one mix, score-banded — the score calculator's
///     "what personal bests look like" section (docs/design/phoenix-score-calculator.md D9).
///     Non-broken Singles/Doubles bests only; levels arrive ascending, gates are the reader's.
///     One grouped read over the whole record table, so the answer caches for hours.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetScorePopulationQuery(MixEnum Mix) : IQuery<IReadOnlyList<LevelScorePopulation>>;
