using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The PIU Scores side of the Hardmode leaderboard, best first. Reads the totals the weekly
///     census wrote, so it is an ordered read rather than a sweep. The Official Boards side is
///     OfficialMirror's own query — the two populations live in different verticals and neither
///     is ranked against the other (docs/design/hardmode-leaderboard.md §4).
///     <para>
///         <paramref name="ViewerId" /> is who is looking, and the board is built for them:
///         private accounts are off it except their own (D17). Null is an anonymous read and
///         gets the public board alone.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHardmodeBoardQuery(MixEnum Mix, ChartType? Pool = null, Guid? ViewerId = null)
    : IQuery<HardmodeBoardRecord>;
