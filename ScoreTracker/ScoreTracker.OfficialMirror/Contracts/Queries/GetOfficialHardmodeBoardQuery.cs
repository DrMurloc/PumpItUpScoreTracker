using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     The Official Boards side of the Hardmode leaderboard, best first (design §4). Its own
///     query rather than a source flag on the site one: the two populations live in different
///     verticals, a board player has no site profile to open, and neither list is ranked against
///     the other.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialHardmodeBoardQuery(MixEnum Mix, ChartType? Pool = null)
    : IQuery<IReadOnlyList<OfficialHardmodeRow>>;
