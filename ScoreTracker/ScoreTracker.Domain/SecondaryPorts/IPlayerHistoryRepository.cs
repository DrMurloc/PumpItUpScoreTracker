using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerHistoryRepository
{
    Task WriteHistory(MixEnum mix, PlayerRatingRecord record, CancellationToken cancellationToken);

    /// <summary>Account-level wipe: clears the user's history across every mix.</summary>
    Task DeleteHistoryForUser(Guid userId, CancellationToken cancellationToken);
}