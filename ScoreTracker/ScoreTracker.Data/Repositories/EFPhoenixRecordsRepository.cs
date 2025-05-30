﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Application.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFPhoenixRecordsRepository : IPhoenixRecordRepository,
    IRequestHandler<GetPlayerChartAggregatesQuery, IEnumerable<UserChartAggregate>>
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

    public async Task UpdateScoreStats(Guid userId, IEnumerable<PhoenixRecordStats> stats,
        CancellationToken cancellationToken = default)
    {
        var statArray = stats.ToArray();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var chartIds = statArray.Select(s => s.ChartId).ToArray();
        var entities = await database.PhoenixRecordStats.Where(s => s.UserId == userId && chartIds.Contains(s.ChartId))
            .ToDictionaryAsync(e => e.ChartId, cancellationToken);
        var toCreate = new List<PhoenixRecordStatsEntity>();
        foreach (var stat in statArray)
            if (entities.ContainsKey(stat.ChartId))
            {
                entities[stat.ChartId].Pumbility = stat.Pumbility;
                entities[stat.ChartId].PumbilityPlus = stat.PumbilityPlus;
            }
            else
            {
                toCreate.Add(new PhoenixRecordStatsEntity
                {
                    ChartId = stat.ChartId,
                    UserId = userId,
                    PumbilityPlus = stat.PumbilityPlus,
                    Pumbility = stat.Pumbility
                });
            }

        await database.PhoenixRecordStats.AddRangeAsync(toCreate, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
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

    public async Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[MixEnum.Phoenix];
        var intLevel = (int)level;
        var chartTypeString = chartType.ToString();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.PhoenixBestAttempt on c.Id equals pba.ChartId
                where cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString && pba.Score == 1000000
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb =>
                (pb.UserId, pb.ChartId));
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

    public async Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        var chartIdArray = chartIds.Distinct().ToArray();
        return await (from pba in _database.PhoenixBestAttempt
                join u in _database.User on pba.UserId equals u.Id
                where chartIdArray.Contains(pba.ChartId) && pba.Score != null && userIdArray.Contains(pba.UserId)
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(
        IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel difficulty,
        CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.ToArray();
        var mixId = MixGuids[MixEnum.Phoenix];
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.PhoenixBestAttempt on c.Id equals pba.ChartId
                where
                    userIdArray.Contains(pba.UserId) &&
                    cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate)));
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
        return (await GetCachedScores(userId, cancellationToken)).Count(c =>
            chartIds.Contains(c.Key) && !c.Value.IsBroken);
    }

    public async Task<IEnumerable<UserChartAggregate>> Handle(GetPlayerChartAggregatesQuery request,
        CancellationToken cancellationToken)
    {
        var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var playerQuery = from u in database.User select u;
        if (request.CommunityName != null)
        {
            var communityNameString = request.CommunityName.Value.ToString();
            playerQuery = from u in playerQuery
                join cm in database.CommunityMembership on u.Id equals cm.UserId
                join c in database.Community on cm.CommunityId equals c.Id
                where c.Name == communityNameString
                select u;
        }
        else
        {
            playerQuery = playerQuery.Where(p => p.IsPublic);
        }

        var chartQuery = from c in database.Chart select c;
        if (request.MaxLevel != null)
        {
            var levelInt = request.MaxLevel.Value;
            chartQuery = chartQuery.Where(c => c.Level <= levelInt);
        }

        if (request.MinLevel != null)
        {
            var levelInt = request.MinLevel.Value;
            chartQuery = chartQuery.Where(c => c.Level >= levelInt);
        }

        if (request.ChartType != null)
        {
            var typeString = request.ChartType.Value.ToString();
            chartQuery = chartQuery.Where(c => c.Type == typeString);
        }

        if (request.ChartMix != null)
        {
            var mixId = MixGuids[request.ChartMix.Value];
            chartQuery = chartQuery.Where(c => c.OriginalMixId == mixId);
        }

        return await (from p in playerQuery
            join pr in database.PhoenixBestAttempt on p.Id equals pr.UserId
            join c in chartQuery on pr.ChartId equals c.Id
            join prs in database.PhoenixRecordStats on new { pr.ChartId, pr.UserId } equals new
                { prs.ChartId, prs.UserId }
            group new { pr, prs } by pr.UserId
            into g
            select new UserChartAggregate(g.Key, g.Count(e => !e.pr.IsBroken), g.Count(),
                (int)g.Average(e => e.pr.Score ?? 0),
                g.Sum(e => e.prs.Pumbility), g.Sum(e => e.prs.PumbilityPlus))).ToArrayAsync(cancellationToken);
    }
}