using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Bus trigger: compute and persist the season recap for one user, or for every user
///     with recorded scores on the mix when <paramref name="UserId" /> is null. Published
///     from the admin dashboard (owner-first, then the one-shot all-players run).
///     Idempotent upsert — safe to re-fire after a process restart.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CalculateSeasonRecapsCommand(Guid? UserId, MixEnum Mix = MixEnum.Phoenix)
{
}
