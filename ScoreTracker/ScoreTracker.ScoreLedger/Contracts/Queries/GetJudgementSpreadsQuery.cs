using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The measured judgement mix per letter grade in one mix — what an S actually looks like,
///     per 1,000 notes (docs/design/phoenix-score-calculator.md D8). Grades resolve from each
///     best's score against the queried mix's floors, not from the stored letter, so legacy
///     grade strings cannot skew a band. Ordered best grade first; every grade with at least
///     one judged play is present and the reader applies its own display gate. Cached for
///     hours like the population census.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetJudgementSpreadsQuery(MixEnum Mix) : IQuery<IReadOnlyList<GradeJudgementSpread>>;
