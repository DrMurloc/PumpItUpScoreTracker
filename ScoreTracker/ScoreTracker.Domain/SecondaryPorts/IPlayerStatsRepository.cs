using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerStatsRepository
{
    Task SaveStats(Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken);
    Task<PlayerStatsRecord> GetStats(Guid userId, CancellationToken cancellationToken);
    Task<IEnumerable<PlayerStatsRecord>> GetStats(IEnumerable<Guid> userIds, CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(ChartType? chartType, double competitiveLevel, double range,
        CancellationToken cancellationToken);
}