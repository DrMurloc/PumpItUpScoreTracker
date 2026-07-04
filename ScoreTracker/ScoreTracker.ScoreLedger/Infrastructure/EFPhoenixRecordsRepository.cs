using System.Collections.Concurrent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFPhoenixRecordsRepository : IPhoenixRecordRepository,
    IScoreReader,
    IRequestHandler<GetPlayerChartAggregatesQuery, IEnumerable<UserChartAggregate>>
{
    // IScoreReader — the Ledger's published read contract. Adapts the internal
    // repository methods; consumers migrate onto these during P3 (F1).
    Task<IEnumerable<RecordedPhoenixScore>> IScoreReader.GetBestScores(Guid userId,
        CancellationToken cancellationToken)
    {
        return GetRecordedScores(userId, cancellationToken);
    }

    async Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> IScoreReader.GetScores(
        ChartType chartType, DifficultyLevel level, CancellationToken cancellationToken)
    {
        return await GetAllPlayerScores(chartType, level, cancellationToken);
    }

    Task<IEnumerable<RecordedPhoenixScore>> IScoreReader.GetScores(IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken)
    {
        return GetRecordedScores(userIds, chartType, minimumLevel, maximumLevel, cancellationToken);
    }

    Task<IEnumerable<(Guid UserId, Guid ChartId)>> IScoreReader.GetPgUsers(ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken)
    {
        return GetPgUsers(chartType, level, cancellationToken);
    }

    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> IScoreReader.GetPlayerScores(
        IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel difficulty,
        CancellationToken cancellationToken)
    {
        return GetPlayerScores(userIds, chartType, difficulty, cancellationToken);
    }

    Task<IEnumerable<UserPhoenixScore>> IScoreReader.GetPlayerScores(IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
    {
        return GetPlayerScores(userIds, chartIds, cancellationToken);
    }

    Task<IEnumerable<UserPhoenixScore>> IScoreReader.GetPhoenixScores(IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken)
    {
        return GetPhoenixScores(userIds, chartId, cancellationToken);
    }

    Task<int> IScoreReader.GetClearCount(Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken)
    {
        return GetClearCount(userId, chartType, level, cancellationToken);
    }

    async Task<IEnumerable<ScoreJournalEntry>> IScoreReader.GetScoreHistory(Guid userId, Guid chartId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.ChartId == chartId)
                .OrderBy(e => e.OccurredAt)
                .ToArrayAsync(cancellationToken))
            .Select(e => new ScoreJournalEntry(e.OccurredAt, e.Source, e.UserId, e.ChartId,
                e.Score, PhoenixPlateHelperMethods.TryParse(e.Plate), e.IsBroken, MixIds.ToEnum(e.MixId)));
    }

    async Task<IReadOnlySet<Guid>> IScoreReader.GetActiveUserIds(DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PhoenixRecordEntity>()
                .Where(pba => pba.RecordedDate >= since)
                .Select(pba => pba.UserId)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
    }

    Task<IEnumerable<BestXXChartAttempt>> IScoreReader.GetBestXXAttempts(Guid userId,
        CancellationToken cancellationToken)
    {
        return _xxAttempts.GetBestAttempts(userId, cancellationToken);
    }

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IChartRepository _charts;
    private readonly IXXChartAttemptRepository _xxAttempts;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;

    private static string ScoreCache(Guid userId)
    {
        return $"{nameof(EFPhoenixRecordsRepository)}_UserScores_{userId}";
    }

    public EFPhoenixRecordsRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IMemoryCache cache,
        IChartRepository charts,
        IXXChartAttemptRepository xxAttempts,
        IMediator mediator,
        IPlayerStatsReader playerStats)
    {
        _cache = cache;
        _factory = factory;
        _charts = charts;
        _xxAttempts = xxAttempts;
        _mediator = mediator;
        _playerStats = playerStats;
    }

    public async Task UpdateBestAttempt(Guid userId, RecordedPhoenixScore score,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing =
            await database.Set<PhoenixRecordEntity>().FirstOrDefaultAsync(
                pba => pba.UserId == userId && pba.ChartId == score.ChartId, cancellationToken);
        if (existing == null)
        {
            await database.AddAsync(new PhoenixRecordEntity
            {
                ChartId = score.ChartId,
                UserId = userId,
                Id = new Guid(),
                // Phoenix until the port takes a mix (plan doc, port-threading commit).
                MixId = MixIds.Phoenix,
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

        await database.SaveChangesAsync(cancellationToken);
        var cache = await GetCachedScores(userId, cancellationToken);
        cache[score.ChartId] = score;
        _cache.Set(ScoreCache(userId), cache);
    }

    private async Task<ConcurrentDictionary<Guid, RecordedPhoenixScore>> GetCachedScores(Guid userId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(ScoreCache(userId), async o =>
        {
            o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<PhoenixRecordEntity>().Where(pba => pba.UserId == userId)
                .Select(pba => new RecordedPhoenixScore(pba.ChartId, pba.Score,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, pba.RecordedDate))
                .ToArrayAsync(cancellationToken);

            return new ConcurrentDictionary<Guid, RecordedPhoenixScore>(
                rows.Select(r => new KeyValuePair<Guid, RecordedPhoenixScore>(r.ChartId, r)));
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
        var mixId = MixIds.Phoenix;
        var intLevel = (int)level;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString && pba.Score == 1000000
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb =>
                (pb.UserId, pb.ChartId));
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken)
    {
        var userIdArray = userIds.ToArray();
        var mixId = MixIds.Phoenix;
        var intMin = (int)minimumLevel;
        var intMax = (int)maximumLevel;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where userIdArray.Contains(pba.UserId)
                      && cm.MixId == mixId && cm.Level >= intMin && cm.Level <= intMax && c.Type == chartTypeString
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
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.Score != null
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartScoreAggregate>> GetAllChartScoreAggregates(CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
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
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
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
        var mixId = MixIds.Phoenix;
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where
                    userIdArray.Contains(pba.UserId) &&
                    cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate)));
    }


    public async Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetAllPlayerScores(ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.Phoenix;
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
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
        var mixId = MixIds.Phoenix;
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        // Competitive-level cohort comes from PlayerProgress's published reader — its
        // PlayerStats table is vertical-internal, so no SQL join onto it from here.
        var cohort = (await _playerStats.GetPlayersByCompetitiveRange(chartType, intLevel, .5, cancellationToken))
            .ToHashSet();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pr in database.Set<PhoenixRecordEntity>() on cm.ChartId equals pr.ChartId
                where cm.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                      && cohort.Contains(pr.UserId)
                select pr).ToArrayAsync(cancellationToken))
            .GroupBy(c => c.ChartId).Select(g => new ChartScoreAggregate(g.Key, g.Count()));
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
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
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var playerQuery = from u in database.User select u;
        if (request.CommunityName != null)
        {
            // Community membership is another vertical's data — resolved through its
            // published contract instead of joining its (internal) tables directly.
            var memberIds = (await _mediator.Send(
                    new GetCommunityMembersQuery(request.CommunityName.Value), cancellationToken))
                .ToArray();
            playerQuery = playerQuery.Where(u => memberIds.Contains(u.Id));
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
            var mixId = MixIds.For(request.ChartMix.Value);
            chartQuery = chartQuery.Where(c => c.OriginalMixId == mixId);
        }

        return await (from p in playerQuery
            join pr in database.Set<PhoenixRecordEntity>() on p.Id equals pr.UserId
            join c in chartQuery on pr.ChartId equals c.Id
            join prs in database.Set<PhoenixRecordStatsEntity>() on new { pr.ChartId, pr.UserId } equals new
                { prs.ChartId, prs.UserId }
            group new { pr, prs } by pr.UserId
            into g
            select new UserChartAggregate(g.Key, g.Count(e => !e.pr.IsBroken), g.Count(),
                (int)g.Average(e => e.pr.Score ?? 0),
                g.Sum(e => e.prs.Pumbility), g.Sum(e => e.prs.PumbilityPlus))).ToArrayAsync(cancellationToken);
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var scores = await database.Set<PhoenixRecordEntity>().Where(p => p.UserId == userId).ToArrayAsync(cancellationToken);
        var stats = await database.Set<PhoenixRecordStatsEntity>().Where(p => p.UserId == userId).ToArrayAsync(cancellationToken);
        database.Set<PhoenixRecordEntity>().RemoveRange(scores);
        database.Set<PhoenixRecordStatsEntity>().RemoveRange(stats);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(ScoreCache(userId));
    }
}
