using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     A player's most recent finished Daily Step days, newest first, capped at <c>Take</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUserDailyStepHistoryQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix, int Take = 14)
    : IQuery<IEnumerable<DailyStepHistoryRecord>>;
