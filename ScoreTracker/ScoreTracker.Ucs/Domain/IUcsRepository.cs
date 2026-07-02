using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Ucs.Contracts;

namespace ScoreTracker.Ucs.Domain;

internal interface IUcsRepository
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
