using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPlayerStatsRepository
{
    Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken);
    Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, CancellationToken cancellationToken);

    Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(MixEnum mix, ChartType? chartType, double competitiveLevel,
        double range, CancellationToken cancellationToken);

    Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, CancellationToken cancellationToken);

    Task DeleteStats(MixEnum mix, Guid userId, CancellationToken cancellationToken);
}