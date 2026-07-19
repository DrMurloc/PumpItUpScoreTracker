using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     The challenges page's Daily Step read: board meta plus ranked, display-ready rows in one
///     dispatch (the widget's raw <c>GetDailyStepQuery</c> + <c>GetDailyStepEntriesQuery</c> pair
///     stays for its own layering). Null when no board is live for the mix.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetDailyStepBoardQuery(MixEnum Mix, Guid? UserId = null) : IQuery<DailyStepBoardView?>;
