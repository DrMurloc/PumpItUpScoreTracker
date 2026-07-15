using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     The monthly leaderboard, aggregated and priced in the vertical (the page previously
///     recomputed this per render from raw week-by-week reads). <c>WeekInMonth</c> counts the
///     window's weeks including a live one; each player's best <c>CountedPerPlayer</c>
///     (4 × <c>WeekInMonth</c>) entries score. <c>WindowStart</c> is null only before the
///     month's first board has any history.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MonthlyLeaderboardView(
    IReadOnlyList<MonthlyLeaderboardRow> Rows,
    int WeekInMonth,
    int CountedPerPlayer,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd);

/// <summary>
///     One player's month. <c>TopFour</c> is the display head; <c>AllCounted</c> is every entry
///     that scored (the expansion view). Ordering inside both is points-descending.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MonthlyLeaderboardRow(
    int Place,
    User? Player,
    double Total,
    IReadOnlyList<MonthlyEntry> TopFour,
    IReadOnlyList<MonthlyEntry> AllCounted);

/// <summary>
///     A counted score with its PUMBILITY price (raw score when the view is Co-Op — see
///     <see cref="Queries.GetMonthlyLeaderboardQuery" />).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MonthlyEntry(Guid ChartId, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken, double Points);
