using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     Player-initiated deletion of the progression stores keyed to scores. Null mix means
///     every mix. Separate from IAccountPurgeRepository because that one takes everything
///     unconditionally for an account that is going away; this one is scoped to what the player
///     chose to delete, and their account survives it.
/// </summary>
internal interface IPlayerScoreDataRepository
{
    Task DeleteHistory(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default);

    Task DeleteHighlights(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default);

    Task DeleteMilestones(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default);

    /// <summary>Drops the highlights and milestones one session produced, for an undo.</summary>
    Task DeleteForSession(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}
