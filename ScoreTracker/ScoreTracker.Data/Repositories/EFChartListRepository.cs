using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartListRepository : IChartListRepository
{
    private readonly ChartAttemptDbContext _dbContext;

    public EFChartListRepository(ChartAttemptDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<SavedChartRecord>> GetSavedChartsByUser(Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SavedChart.Where(sc => sc.UserId == userId)
            .Select(sc => new SavedChartRecord(Enum.Parse<ChartListType>(sc.ListName), sc.ChartId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveChart(Guid userId, ChartListType listType, Guid chartId, CancellationToken cancellationToken)
    {
        var existingRecord = await _dbContext.SavedChart.FirstOrDefaultAsync(
            sc => sc.UserId == userId && sc.ChartId == chartId && sc.ListName == listType.ToString(),
            cancellationToken);

        if (existingRecord == null)
        {
            await _dbContext.SavedChart.AddAsync(new SavedChartEntity
            {
                Id = Guid.NewGuid(),
                ChartId = chartId,
                ListName = listType.ToString(),
                UserId = userId
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveChart(Guid userId, ChartListType listType, Guid chartId,
        CancellationToken cancellationToken)
    {
        var existingRecord = await _dbContext.SavedChart.FirstOrDefaultAsync(
            sc => sc.UserId == userId && sc.ChartId == chartId && sc.ListName == listType.ToString(),
            cancellationToken);

        if (existingRecord != null)
        {
            _dbContext.SavedChart.Remove(existingRecord);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}