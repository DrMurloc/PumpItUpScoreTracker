using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     The Hardmode tab for one viewer and one pool. The pool's charts are priced at request
///     time from the viewer's own records against the week's frozen list, so the number moves
///     with their imports while the list holds until Sunday
///     (docs/design/hardmode-leaderboard.md §8).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHardmodePageQuery(Guid UserId, MixEnum Mix, ChartType? Pool = null)
    : IQuery<HardmodePageRecord>;
