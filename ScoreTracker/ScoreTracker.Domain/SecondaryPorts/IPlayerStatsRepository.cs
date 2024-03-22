using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerStatsRepository
{
    Task SaveStats(Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken);
}