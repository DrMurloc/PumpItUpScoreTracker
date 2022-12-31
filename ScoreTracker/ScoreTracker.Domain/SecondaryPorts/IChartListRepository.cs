using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartListRepository
{
    Task<IEnumerable<SavedChartRecord>> GetSavedChartsByUser(Guid userId, CancellationToken cancellationToken);
    Task SaveChart(Guid userId, ChartListType listType, Guid chartId, CancellationToken cancellationToken);
    Task RemoveChart(Guid userId, ChartListType listType, Guid chartId, CancellationToken cancellationToken);
}