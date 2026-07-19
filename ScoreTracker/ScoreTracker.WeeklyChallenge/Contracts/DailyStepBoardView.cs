using ScoreTracker.Domain.Models;

namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     Today's Daily Step board, display-ready for the challenges page. Rows are ranked by the
///     shared placement policy (ascending passes-only on Limbo, score-descending otherwise —
///     the same ranking the rotation snapshots and <c>GetDailyStepPlacementQuery</c> reports).
///     <c>MyRow</c> is the caller's row when they sit on the board.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepBoardView(
    DailyStepBoard Board,
    IReadOnlyList<DailyStepBoardRow> Rows,
    DailyStepBoardRow? MyRow);

/// <summary>A ranked Daily Step row. <c>Player</c> is null for a deleted account.</summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepBoardRow(int Place, User? Player, DailyStepEntry Entry);
