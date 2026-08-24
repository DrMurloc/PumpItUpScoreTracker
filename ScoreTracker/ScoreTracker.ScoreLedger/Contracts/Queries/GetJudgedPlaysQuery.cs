using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     A player's journal rows that carry a full judgement breakdown, newest first — the score
///     calculator's "load one of your plays" list (docs/design/phoenix-score-calculator.md D7).
///     Stage breaks are excluded (their judgements stop mid-chart and describe no complete
///     screen); finished fails are included, because a broken run that reached the last note is
///     a complete result screen. Only recent-plays imports carry judgements, so this list is
///     shorter than the journal itself.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetJudgedPlaysQuery(Guid UserId, MixEnum Mix, int Limit = 300)
    : IQuery<IReadOnlyList<ScoreJournalEntry>>;
