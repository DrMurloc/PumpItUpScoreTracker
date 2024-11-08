using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IUcsRepository
    {
        Task<IEnumerable<UcsChart>> GetUcsCharts(CancellationToken cancellationToken);
        Task CreateUcsChart(UcsChart chart, CancellationToken cancellationToken);
        Task<IEnumerable<UcsLeaderboardEntry>> GetChartLeaderboard(Guid chartId, CancellationToken cancellationToken);

        Task UpdateScore(Guid chartId, Guid userId, PhoenixScore score, PhoenixPlate plate, bool isBroken,
            Uri? videoPath,
            Uri? imagePath, CancellationToken cancellationToken);

        Task<IEnumerable<ChartTagAggregate>> GetChartTags(CancellationToken cancellationToken);
        Task DeleteChartTag(Guid chartId, Guid userId, Name tag, CancellationToken cancellationToken);
        Task AddChartTag(Guid chartId, Guid userId, Name tag, CancellationToken cancellationToken);
        Task<IEnumerable<Name>> GetMyTags(Guid chartId, Guid userId, CancellationToken cancellationToken);
        Task<IEnumerable<UserChartTag>> GetAllMyTags(Guid userId, CancellationToken cancellationToken);
    }
}
