using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartAttemptRepository : IChartAttemptRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFChartAttemptRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task<ChartAttempt?> GetBestAttempt(Guid userId, Chart chart,
        CancellationToken cancellationToken = default)
    {
        var chartId = await GetChartId(chart, cancellationToken);
        return await (
            from ba in _database.BestAttempt
            where ba.ChartId == chartId && ba.UserId == userId
            select new ChartAttempt(Enum.Parse<LetterGrade>(ba.LetterGrade), ba.IsBroken)
        ).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RemoveBestAttempt(Guid userId, Chart chart, CancellationToken cancellationToken = default)
    {
        var chartId = await GetChartId(chart, cancellationToken);

        var previousBest = await _database.BestAttempt.Where(ba => ba.ChartId == chartId && ba.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (previousBest != null)
        {
            _database.BestAttempt.Remove(previousBest);
            await _database.SaveChangesAsync(cancellationToken);
        }
    }


    public async Task SetBestAttempt(Guid userId, Chart chart, ChartAttempt attempt, DateTimeOffset recordedOn,
        CancellationToken cancellationToken = default)
    {
        var chartId = await GetChartId(chart, cancellationToken);

        var previousBest = await _database.BestAttempt.Where(ba => ba.ChartId == chartId && ba.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
        if (previousBest == null)
        {
            var entity = new BestAttemptEntity
            {
                ChartId = chartId,
                Id = Guid.NewGuid(),
                IsBroken = attempt.IsBroken,
                LetterGrade = attempt.LetterGrade.ToString(),
                RecordedDate = recordedOn,
                UserId = userId
            };
            await _database.BestAttempt.AddAsync(entity, cancellationToken);
        }
        else
        {
            previousBest.LetterGrade = attempt.LetterGrade.ToString();
            previousBest.RecordedDate = recordedOn;
            previousBest.IsBroken = attempt.IsBroken;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> GetChartId(Chart chart, CancellationToken cancellationToken)
    {
        var songString = (string)chart.SongName;
        var levelInt = (int)chart.Level;
        var typeString = chart.Type.ToString();

        return await (from s in _database.Song
            join c in _database.Chart on s.Id equals c.SongId
            where s.Name == songString && c.Level == levelInt && c.Type == typeString
            select c.Id).SingleAsync(cancellationToken);
    }
}