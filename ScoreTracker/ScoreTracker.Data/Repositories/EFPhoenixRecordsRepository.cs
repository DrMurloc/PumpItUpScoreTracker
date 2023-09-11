using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFPhoenixRecordsRepository : IPhoenixRecordRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFPhoenixRecordsRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task UpdateBestAttempt(Guid userId, RecordedPhoenixScore score,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await _database.PhoenixBestAttempt.FirstOrDefaultAsync(
                pba => pba.UserId == userId && pba.ChartId == score.ChartId, cancellationToken);
        if (existing == null)
        {
            await _database.AddAsync(new PhoenixRecordEntity
            {
                ChartId = score.ChartId,
                UserId = userId,
                Id = new Guid(),
                IsBroken = score.IsBroken,
                Score = score.Score,
                LetterGrade = score.Score?.LetterGrade.GetName(),
                Plate = score.Plate?.GetName(),
                RecordedDate = score.RecordedDate
            }, cancellationToken);
        }
        else
        {
            existing.Score = score.Score;
            existing.LetterGrade = score.Score?.LetterGrade.GetName();
            existing.Plate = score.Plate?.GetName();
            existing.IsBroken = score.IsBroken;
            existing.RecordedDate = score.RecordedDate;
        }

        await _database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.PhoenixBestAttempt.Where(pba => pba.UserId == userId)
            .Select(pba => new RecordedPhoenixScore(pba.ChartId, pba.Score,
                PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, pba.RecordedDate))
            .ToArrayAsync(cancellationToken);

        return result;
    }

    public async Task<RecordedPhoenixScore?> GetRecordedScore(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var result =
            await _database.PhoenixBestAttempt.FirstOrDefaultAsync(
                pba => pba.UserId == userId && pba.ChartId == chartId, cancellationToken);

        if (result == null) return null;

        return new RecordedPhoenixScore(result.ChartId, result.Score, PhoenixPlateHelperMethods.TryParse(result.Plate),
            result.IsBroken,
            result.RecordedDate);
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetRecordedUserScores(Guid chartId,
        CancellationToken cancellationToken = default)
    {
        return await (from pba in _database.PhoenixBestAttempt
                join u in _database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.Score != null
                select new UserPhoenixScore(pba.ChartId, u.IsPublic ? u.Name : "Anonymous", pba.Score.Value))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartScoreAggregate>> GetAllChartScoreAggregates(CancellationToken cancellationToken)
    {
        return await (from pba in _database.PhoenixBestAttempt
            where pba.Score != null
            group pba by pba.ChartId
            into g
            select new ChartScoreAggregate(g.Key, g.Count())).ToArrayAsync(cancellationToken);
    }
}