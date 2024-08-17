using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFPhoenixRecordsRepository : IPhoenixRecordRepository
{
    private readonly ChartAttemptDbContext _database;
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _dbFactory;
    private readonly IChartRepository _charts;

    private static string ScoreCache(Guid userId)
    {
        return $"{nameof(EFPhoenixRecordsRepository)}_UserScores_{userId}";
    }

    public EFPhoenixRecordsRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IMemoryCache cache,
        IDbContextFactory<ChartAttemptDbContext> dbFactory,
        IChartRepository charts)
    {
        _database = factory.CreateDbContext();
        _cache = cache;
        _dbFactory = dbFactory;
        _charts = charts;
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
        var cache = await GetCachedScores(userId, cancellationToken);
        cache[score.ChartId] = score;
        _cache.Set(ScoreCache(userId), cache);
    }

    private async Task<IDictionary<Guid, RecordedPhoenixScore>> GetCachedScores(Guid userId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(ScoreCache(userId), async o =>
        {
            o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(10);
            var result = (await _database.PhoenixBestAttempt.Where(pba => pba.UserId == userId)
                .Select(pba => new RecordedPhoenixScore(pba.ChartId, pba.Score,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, pba.RecordedDate))
                .ToArrayAsync(cancellationToken)).ToDictionary(r => r.ChartId);

            return result;
        });
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (await GetCachedScores(userId, cancellationToken)).Values;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(IEnumerable<Guid> userId,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken)
    {
        var mixId = MixGuids[MixEnum.Phoenix];
        var intMin = (int)minimumLevel;
        var intMax = (int)maximumLevel;
        var chartTypeString = chartType.ToString();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.PhoenixBestAttempt on c.Id equals pba.ChartId
                where cm.MixId == mixId && cm.Level >= intMin && cm.Level <= intMax && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb =>
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate));
    }

    public async Task<RecordedPhoenixScore?> GetRecordedScore(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        return (await GetCachedScores(userId, cancellationToken)).TryGetValue(chartId, out var r) ? r : null;
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetRecordedUserScores(Guid chartId,
        CancellationToken cancellationToken = default)
    {
        return await (from pba in _database.PhoenixBestAttempt
                join u in _database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.Score != null
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken))
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

    //Will  need to refactor this if I ever support non prod environments
    //Mostly saving some tedious joins for now.
    private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
    {
        { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
        { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
    };

    public async Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetAllPlayerScores(ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[MixEnum.Phoenix];
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.PhoenixBestAttempt on c.Id equals pba.ChartId
                where cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate)));
    }

    public async Task<IEnumerable<ChartScoreAggregate>> GetMeaningfulScoresCount(ChartType chartType,
        DifficultyLevel difficulty,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[MixEnum.Phoenix];
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pr in database.PhoenixBestAttempt on cm.ChartId equals pr.ChartId
                join ps in database.PlayerStats on pr.UserId equals ps.UserId
                where cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                      && ((chartTypeString == "Single" && ps.SinglesCompetitiveLevel >= cm.Level - .5 &&
                           ps.SinglesCompetitiveLevel <= cm.Level + .5) || (chartTypeString == "Double" &&
                                                                            ps.DoublesCompetitiveLevel >=
                                                                            cm.Level - .5 &&
                                                                            ps.DoublesCompetitiveLevel <=
                                                                            cm.Level + .5))
                select pr).ToArrayAsync(cancellationToken))
            .GroupBy(c => c.ChartId).Select(g => new ChartScoreAggregate(g.Key, g.Count()));
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        return await (from pba in _database.PhoenixBestAttempt
                join u in _database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.Score != null && userIdArray.Contains(pba.UserId)
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> GetClearCount(Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default)
    {
        var chartIds = (await _charts.GetCharts(MixEnum.Phoenix, level, chartType, null, cancellationToken))
            .Select(c => c.Id).Distinct().ToHashSet();
        return (await GetCachedScores(userId, cancellationToken)).Count(c => chartIds.Contains(c.Key));
    }
}