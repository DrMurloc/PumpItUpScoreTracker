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

    public async Task<IEnumerable<BestChartAttempt>> GetBestAttempts(Guid userId, IEnumerable<Chart> charts,
        CancellationToken cancellationToken)
    {
        var result = (from ce in charts
            join s in _database.Song on (string)ce.Song.Name equals s.Name
            join c in _database.Chart on new
                    { SongId = s.Id, Level = (int)ce.Level, ChartType = ce.Type.ToString() } equals
                new { c.SongId, c.Level, ChartType = c.Type }
            join _ in _database.BestAttempt on c.Id equals _.ChartId into gi
            from ba in gi.DefaultIfEmpty()
            select new BestChartAttempt(
                new Chart(new Song(s.Name, new Uri(s.ImagePath)), Enum.Parse<ChartType>(c.Type), c.Level),
                ba == null ? null : new ChartAttempt(Enum.Parse<LetterGrade>(ba.LetterGrade), ba.IsBroken))).ToArray();
        return result;
    }

    private async Task<Guid> GetChartId(Chart chart, CancellationToken cancellationToken)
    {
        var songString = (string)chart.Song.Name;
        var levelInt = (int)chart.Level;
        var typeString = chart.Type.ToString();

        return await (from s in _database.Song
            join c in _database.Chart on s.Id equals c.SongId
            where s.Name == songString && c.Level == levelInt && c.Type == typeString
            select c.Id).SingleAsync(cancellationToken);
    }
}