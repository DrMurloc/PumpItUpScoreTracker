using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     The monthly board for the month containing <c>AnchorWeek</c> (a past rotation date from
///     <c>GetPastWeeklyDatesQuery</c>; null = the current month, which also counts the live
///     board). Weeks belong to the month their board *started* in (rotation date − 7 days).
///     Pricing is the mix's own PUMBILITY (<c>ScoringConfiguration.PumbilityScoring</c>) —
///     brokens price at zero, per the game. <c>Type</c> null is the Combined view and excludes
///     co-op, matching Phoenix 2's own rule; <c>Type</c> = CoOp ranks by raw score, the only
///     currency co-op charts share. <c>OnlyUserIds</c> scopes the board to a community.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMonthlyLeaderboardQuery(
    MixEnum Mix,
    DateTimeOffset? AnchorWeek = null,
    ChartType? Type = null,
    IReadOnlyList<Guid>? OnlyUserIds = null) : IQuery<MonthlyLeaderboardView>;
