using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

internal interface IPlayerMilestoneRepository
{
    Task Append(MixEnum mix, Guid userId, IEnumerable<PlayerMilestoneWrite> milestones,
        CancellationToken cancellationToken);

    Task<IEnumerable<PlayerMilestoneRecord>> GetMilestones(MixEnum mix, Guid userId, DateTimeOffset since,
        DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Reads session-attached milestones for specific sessions (FK by SessionId).</summary>
    Task<IEnumerable<PlayerMilestoneRecord>> GetMilestonesBySessions(Guid userId, IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken);
}

internal sealed record PlayerMilestoneWrite(
    MilestoneKind Kind,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    double? OldValue = null,
    double? NewValue = null,
    string? Title = null,
    string? Detail = null);
