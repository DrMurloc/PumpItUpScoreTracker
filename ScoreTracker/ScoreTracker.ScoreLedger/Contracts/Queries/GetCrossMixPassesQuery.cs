using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     Chart ids the player has passed in any mix OTHER than the given one — drives the
///     tier-list page's dashed-green "passed in another mix" border. UserId defaults to
///     the current user; reads of another player honor the profile-privacy access gate
///     and return empty when denied.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCrossMixPassesQuery(MixEnum ExcludingMix, Guid? UserId = null)
    : IQuery<IReadOnlySet<Guid>>
{
}
