using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     The full ranked board for one live weekly chart, rows carrying their trust source — what
///     the challenges page's leaderboard dialog reads when a chart is opened (the shared
///     <c>LeaderboardDialog</c> re-ranks and looks up players itself, but needs the source per
///     row to draw the ✔/📷 ladder, which the plain <c>GetWeeklyChartEntriesQuery</c> omits).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyChartBoardQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IReadOnlyList<WeeklyBoardRow>>;
