using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>
///     The player's CURRENT placement on any of the given charts that sit on the active
///     weekly board. The session-snapshot Discord card reads this at render time to flex
///     weekly play (design doc revision 2) — board registration itself stays behind this
///     vertical's eligibility policy (official imports + photo submissions), never the
///     score batches.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetUserWeeklyPlacementsQuery(Guid UserId, MixEnum Mix, IReadOnlyList<Guid> ChartIds)
    : IQuery<IEnumerable<WeeklyPlacementRecord>>;
