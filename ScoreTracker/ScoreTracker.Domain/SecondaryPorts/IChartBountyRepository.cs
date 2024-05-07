using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartBountyRepository
{
    Task<IEnumerable<ChartBounty>> GetChartBounties(ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken);

    Task SetChartBounty(Guid chartId, int worth, CancellationToken cancellationToken);

    Task ClearMonthlyBoard(CancellationToken cancellationToken);
    Task RedeemBounty(Guid userId, int worth, CancellationToken cancellationToken);
    Task<BountyLeaderboard> GetBountyLeaderboard(Guid userId, CancellationToken cancellationToken);
    Task<IEnumerable<BountyLeaderboard>> GetBountyLeaderboard(CancellationToken cancellationToken);
}