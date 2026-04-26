using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartListRepository : IChartListRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartListRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<SavedChartRecord>> GetSavedChartsByUser(Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.SavedChart.Where(sc => sc.UserId == userId)
            .Select(sc => new SavedChartRecord(Enum.Parse<ChartListType>(sc.ListName), sc.ChartId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveChart(Guid userId, ChartListType listType, Guid chartId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existingRecord = await database.SavedChart.FirstOrDefaultAsync(
            sc => sc.UserId == userId && sc.ChartId == chartId && sc.ListName == listType.ToString(),
            cancellationToken);

        if (existingRecord == null)
        {
            await database.SavedChart.AddAsync(new SavedChartEntity
            {
                Id = Guid.NewGuid(),
                ChartId = chartId,
                ListName = listType.ToString(),
                UserId = userId
            }, cancellationToken);

            await database.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveChart(Guid userId, ChartListType listType, Guid chartId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existingRecord = await database.SavedChart.FirstOrDefaultAsync(
            sc => sc.UserId == userId && sc.ChartId == chartId && sc.ListName == listType.ToString(),
            cancellationToken);

        if (existingRecord != null)
        {
            database.SavedChart.Remove(existingRecord);

            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
