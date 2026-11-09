using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

internal interface IPlayerMilestoneRepository
{
    Task Append(MixEnum mix, Guid userId, IEnumerable<PlayerMilestoneWrite> milestones,
        CancellationToken cancellationToken);

    Task<IEnumerable<PlayerMilestoneRecord>> GetMilestones(MixEnum mix, Guid userId, DateTimeOffset since,
        DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Whether the player has ever been given a milestone of this kind on the mix.</summary>
    Task<bool> HasMilestone(MixEnum mix, Guid userId, MilestoneKind kind, CancellationToken cancellationToken);

    /// <summary>Reads session-attached milestones for specific sessions (FK by SessionId).</summary>
    Task<IEnumerable<PlayerMilestoneRecord>> GetMilestonesBySessions(Guid userId, IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Every title completion + full-folder clear (FolderPassLamp) across all users on or after the
    ///     cutoff — the community-feed backfill source.
    /// </summary>
    Task<IEnumerable<(Guid UserId, MixEnum Mix, PlayerMilestoneRecord Record)>> GetFeedMilestonesSince(
        DateTimeOffset since, CancellationToken cancellationToken);
}

internal sealed record PlayerMilestoneWrite(
    MilestoneKind Kind,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    double? OldValue = null,
    double? NewValue = null,
    string? Title = null,
    string? Detail = null);
