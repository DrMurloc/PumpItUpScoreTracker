using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The PIU Scores side of the Hardmode leaderboard, best first. Reads the totals the weekly
///     census wrote, so it is an ordered read rather than a sweep. The Official Boards side is
///     OfficialMirror's own query — the two populations live in different verticals and neither
///     is ranked against the other (docs/design/hardmode-leaderboard.md §4).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHardmodeBoardQuery(MixEnum Mix, ChartType? Pool = null)
    : IQuery<IReadOnlyList<HardmodeBoardRow>>;
