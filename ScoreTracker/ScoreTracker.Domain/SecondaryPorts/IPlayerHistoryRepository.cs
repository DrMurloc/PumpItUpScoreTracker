using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerHistoryRepository
{
    Task WriteHistory(PlayerRatingRecord record, CancellationToken cancellationToken);
}