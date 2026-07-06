using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

internal interface IPlayerMilestoneRepository
{
    Task Append(MixEnum mix, Guid userId, IEnumerable<PlayerMilestoneWrite> milestones,
        CancellationToken cancellationToken);

    Task<IEnumerable<PlayerMilestoneRecord>> GetMilestones(MixEnum mix, Guid userId, DateTimeOffset since,
        DateTimeOffset until, CancellationToken cancellationToken);
}

internal sealed record PlayerMilestoneWrite(
    MilestoneKind Kind,
    Guid? SessionId,
    DateTimeOffset OccurredAt,
    double? OldValue = null,
    double? NewValue = null,
    string? Title = null,
    string? Detail = null);
