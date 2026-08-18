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
using ScoreTracker.ScoreLedger.Contracts;
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
    Task<IEnumerable<RecordedPhoenixScore>> IScoreReader.GetBestScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        return GetRecordedScores(mix, userId, cancellationToken);
    }

    async Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> IScoreReader.GetScores(
        MixEnum mix, ChartType chartType, DifficultyLevel level, CancellationToken cancellationToken)
    {
        return await GetAllPlayerScores(mix, chartType, level, cancellationToken);
    }

    async Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> IScoreReader.GetChartScores(
        MixEnum mix, Guid chartId, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        // One chart, straight off the ChartId index — no folder scan, no joins.
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PhoenixRecordEntity>()
                .Where(pr => pr.ChartId == chartId && pr.MixId == mixId)
                .ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate, Judgements: JudgementsOf(pb))));
    }

    Task<IEnumerable<RecordedPhoenixScore>> IScoreReader.GetScores(MixEnum mix, IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken)
    {
        return GetRecordedScores(mix, userIds, chartType, minimumLevel, maximumLevel, cancellationToken);
    }

    Task<IEnumerable<(Guid UserId, Guid ChartId)>> IScoreReader.GetPgUsers(MixEnum mix, ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken)
    {
        return GetPgUsers(mix, chartType, level, cancellationToken);
    }

    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> IScoreReader.GetPlayerScores(
        MixEnum mix, IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel difficulty,
        CancellationToken cancellationToken)
    {
        return GetPlayerScores(mix, userIds, chartType, difficulty, cancellationToken);
    }

    Task<IEnumerable<UserPhoenixScore>> IScoreReader.GetPlayerScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
    {
        return GetPlayerScores(mix, userIds, chartIds, cancellationToken);
    }

    Task<IEnumerable<UserPhoenixScore>> IScoreReader.GetPlayerScoresInLevelRange(MixEnum mix,
        IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel minimumLevel,
        DifficultyLevel maximumLevel, CancellationToken cancellationToken)
    {
        return GetPlayerScoresInLevelRange(mix, userIds, chartType, minimumLevel, maximumLevel,
            cancellationToken);
    }

    Task<IEnumerable<UserPhoenixScore>> IScoreReader.GetPhoenixScores(MixEnum mix, IEnumerable<Guid> userIds,
        Guid chartId,
        CancellationToken cancellationToken)
    {
        return GetPhoenixScores(mix, userIds, chartId, cancellationToken);
    }

    Task<int> IScoreReader.GetClearCount(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken)
    {
        return GetClearCount(mix, userId, chartType, level, cancellationToken);
    }

    async Task<IEnumerable<ScoreJournalEntry>> IScoreReader.GetScoreHistory(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.ChartId == chartId && e.MixId == mixId)
                .OrderBy(e => e.OccurredAt)
                .ToArrayAsync(cancellationToken))
            .Select(e => new ScoreJournalEntry(e.OccurredAt, e.Source, e.UserId, e.ChartId,
                e.Score, PhoenixPlateHelperMethods.TryParse(e.Plate), e.IsBroken, MixIds.ToEnum(e.MixId),
                Judgements: EFScoreJournalRepository.JudgementsOf(e), IsBest: e.IsBest,
                IsStageBroken: e.IsStageBroken));
    }

    async Task<IReadOnlySet<Guid>> IScoreReader.GetActiveUserIds(MixEnum mix, DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PhoenixRecordEntity>()
                .Where(pba => pba.MixId == mixId && pba.RecordedDate >= since)
                .Select(pba => pba.UserId)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
    }

    async Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> IScoreReader.GetVerifiedBests(MixEnum mix,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return Array.Empty<(Guid, RecordedPhoenixScore)>();

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // NULL source is pre-capture and counts as verified: nothing has written a null since
        // 2026-07-06, and among the null rows the journal can classify, official imports beat
        // manual and CSV plays about twenty to one. A row a human typed is the thing being
        // excluded, and those are stamped.
        return (await database.Set<PhoenixRecordEntity>()
                .Where(pba => pba.MixId == mixId
                              && userIds.Contains(pba.UserId)
                              && !pba.IsBroken
                              && pba.Score != null
                              && (pba.Source == null || pba.Source == ScoreJournalEntry.OfficialImportSource))
                .Select(pba => new
                {
                    pba.UserId, pba.ChartId, pba.Score, pba.Plate, pba.IsBroken, pba.RecordedDate, pba.Source
                })
                .ToArrayAsync(cancellationToken))
            .Select(pba => (pba.UserId, new RecordedPhoenixScore(pba.ChartId, pba.Score,
                PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, pba.RecordedDate, pba.Source)))
            .ToArray();
    }

    async Task<IReadOnlyList<(Guid UserId, DateTimeOffset LastRecordedAt)>> IScoreReader.GetVerifiedRecordActivity(
        MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PhoenixRecordEntity>()
                .Where(pba => pba.MixId == mixId
                              && !pba.IsBroken
                              && pba.Score != null
                              && (pba.Source == null || pba.Source == ScoreJournalEntry.OfficialImportSource))
                .GroupBy(pba => pba.UserId)
                .Select(g => new { UserId = g.Key, Last = g.Max(pba => pba.RecordedDate) })
                .ToArrayAsync(cancellationToken))
            .Select(x => (x.UserId, x.Last))
            .ToArray();
    }

    Task<IEnumerable<ChartScoreAggregate>> IScoreReader.GetChartScoreAggregates(MixEnum mix,
        CancellationToken cancellationToken)
    {
        return GetAllChartScoreAggregates(mix, cancellationToken);
    }

    async Task<int> IScoreReader.GetPlayDayCount(MixEnum mix, Guid userId, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId)
            .Select(e => e.OccurredAt.Date)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    async Task<IReadOnlyDictionary<Guid, int>> IScoreReader.GetJournaledChartCounts(MixEnum mix,
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        var ids = userIds.Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var counts = new Dictionary<Guid, int>();
        // Chunked so a big community never overruns the SQL parameter ceiling.
        foreach (var chunk in ids.Chunk(1000))
        {
            var rows = await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.MixId == mixId && chunk.Contains(e.UserId))
                .GroupBy(e => e.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Select(e => e.ChartId).Distinct().Count() })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows) counts[row.UserId] = row.Count;
        }

        return counts;
    }

    Task<IEnumerable<BestXXChartAttempt>> IScoreReader.GetBestXXAttempts(Guid userId,
        CancellationToken cancellationToken)
    {
        // The published IScoreReader surface is XX-specific by name; legacy-mix reads
        // go through GetXXBestChartAttemptsQuery with an explicit mix.
        return _xxAttempts.GetBestAttempts(userId, MixEnum.XX, cancellationToken);
    }

    Task<IEnumerable<BestXXChartAttempt>> IScoreReader.GetBestXXAttempts(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        return _xxAttempts.GetBestAttempts(userId, mix, cancellationToken);
    }

    async Task<IEnumerable<UserLegacyScore>> IScoreReader.GetPlayerLegacyScores(MixEnum mix,
        IEnumerable<Guid> userIds, IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
    {
        var ids = userIds as Guid[] ?? userIds.ToArray();
        var charts = chartIds as Guid[] ?? chartIds.ToArray();
        if (ids.Length == 0 || charts.Length == 0) return Array.Empty<UserLegacyScore>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        // Masked exactly as the Phoenix twin masks: a private player's name never leaves here
        // in the clear, and IsPublic says so outright so no consumer has to recognise the mask.
        var rows = await (from b in database.Set<BestAttemptEntity>()
            where b.MixId == mixId && ids.Contains(b.UserId) && charts.Contains(b.ChartId)
            join u in database.User on b.UserId equals u.Id
            select new { b.UserId, b.ChartId, u.Name, u.IsPublic, b.LetterGrade, b.Score, b.IsBroken, b.RecordedDate })
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(r => Enum.TryParse<XXLetterGrade>(r.LetterGrade, out _))
            .Select(r => new UserLegacyScore(r.UserId, r.ChartId,
                r.IsPublic ? Name.From(r.Name) : Name.From("Anonymous"),
                Enum.Parse<XXLetterGrade>(r.LetterGrade), r.Score, r.IsBroken, r.IsPublic,
                r.RecordedDate))
            .ToArray();
    }

    async Task<IReadOnlyDictionary<Guid, LegacyScoreTotals>> IScoreReader.GetLegacyTotals(MixEnum mix,
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds as Guid[] ?? userIds.ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, LegacyScoreTotals>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        // One grouped pass: the sum and the four tallies come off the same scan. The cast to
        // long happens in SQL — a full-catalogue player's era scores overflow int, which is
        // exactly how the old boards were won.
        var rows = await database.Set<BestAttemptEntity>()
            .Where(b => b.MixId == mixId && ids.Contains(b.UserId))
            .GroupBy(b => b.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                NetScore = g.Sum(b => (long?)b.Score) ?? 0L,
                Scored = g.Count(b => b.Score != null),
                Recorded = g.Count(),
                Passed = g.Count(b => !b.IsBroken),
                // Passing only. A broken run still carries a letter — a broken SSS is possible
                // on a mission zone — but a grade you did not clear is not one to tally, and the
                // player page's folder graphs one click away have always counted it this way.
                TripleS = g.Count(b => b.LetterGrade == "SSS" && !b.IsBroken),
                DoubleS = g.Count(b => b.LetterGrade == "SS" && !b.IsBroken),
                SingleS = g.Count(b => b.LetterGrade == "S" && !b.IsBroken),
                A = g.Count(b => b.LetterGrade == "A" && !b.IsBroken)
            })
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(r => r.UserId,
            r => new LegacyScoreTotals(r.UserId, r.NetScore, r.Scored, r.Recorded, r.Passed,
                r.TripleS, r.DoubleS, r.SingleS, r.A));
    }

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IChartRepository _charts;
    private readonly IXXChartAttemptRepository _xxAttempts;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;

    // Internal so the purge repository evicts under the identical key rather than
    // reconstructing the format and drifting from it.
    internal static string ScoreCache(Guid userId, MixEnum mix)
    {
        return $"{nameof(EFPhoenixRecordsRepository)}_UserScores_{userId}_{mix}";
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

    internal static JudgementCounts? JudgementsOf(PhoenixRecordEntity pba)
    {
        return pba.Perfects == null
            ? null
            : new JudgementCounts(pba.Perfects.Value, pba.Greats!.Value, pba.Goods!.Value, pba.Bads!.Value,
                pba.Misses!.Value, pba.MaxCombo);
    }

    private static void SetJudgements(PhoenixRecordEntity entity, JudgementCounts? judgements)
    {
        entity.Perfects = judgements?.Perfects;
        entity.Greats = judgements?.Greats;
        entity.Goods = judgements?.Goods;
        entity.Bads = judgements?.Bads;
        entity.Misses = judgements?.Misses;
        entity.MaxCombo = judgements?.MaxCombo;
    }

    public async Task UpdateBestAttempt(MixEnum mix, Guid userId, RecordedPhoenixScore score,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing =
            await database.Set<PhoenixRecordEntity>().FirstOrDefaultAsync(
                pba => pba.UserId == userId && pba.ChartId == score.ChartId && pba.MixId == mixId,
                cancellationToken);
        if (existing == null)
        {
            var entity = new PhoenixRecordEntity
            {
                ChartId = score.ChartId,
                UserId = userId,
                Id = new Guid(),
                MixId = mixId,
                IsBroken = score.IsBroken,
                Score = score.Score,
                LetterGrade = score.Score?.LetterGradeFor(mix).GetName(),
                Plate = score.Plate?.GetName(),
                RecordedDate = score.RecordedDate,
                Source = score.Source
            };
            SetJudgements(entity, score.Judgements);
            await database.AddAsync(entity, cancellationToken);
        }
        else
        {
            existing.Score = score.Score;
            existing.LetterGrade = score.Score?.LetterGradeFor(mix).GetName();
            existing.Plate = score.Plate?.GetName();
            existing.IsBroken = score.IsBroken;
            existing.RecordedDate = score.RecordedDate;
            existing.Source = score.Source;
            SetJudgements(existing, score.Judgements);
        }

        await database.SaveChangesAsync(cancellationToken);
        var cache = await GetCachedScores(mix, userId, cancellationToken);
        cache[score.ChartId] = score;
        _cache.Set(ScoreCache(userId, mix), cache);
    }

    private async Task<ConcurrentDictionary<Guid, RecordedPhoenixScore>> GetCachedScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(ScoreCache(userId, mix), async o =>
        {
            o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
            var mixId = MixIds.For(mix);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<PhoenixRecordEntity>()
                .Where(pba => pba.UserId == userId && pba.MixId == mixId)
                .Select(pba => new RecordedPhoenixScore(pba.ChartId, pba.Score,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, pba.RecordedDate, pba.Source,
                    JudgementsOf(pba)))
                .ToArrayAsync(cancellationToken);

            return new ConcurrentDictionary<Guid, RecordedPhoenixScore>(
                rows.Select(r => new KeyValuePair<Guid, RecordedPhoenixScore>(r.ChartId, r)));
        });
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (await GetCachedScores(mix, userId, cancellationToken)).Values;
    }

    public async Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(MixEnum mix, ChartType chartType,
        DifficultyLevel level,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        var intLevel = (int)level;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where cm.MixId == mixId && pba.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString &&
                      pba.Score == 1000000
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb =>
                (pb.UserId, pb.ChartId));
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(MixEnum mix, IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        CancellationToken cancellationToken)
    {
        var userIdArray = userIds.ToArray();
        var mixId = MixIds.For(mix);
        var intMin = (int)minimumLevel;
        var intMax = (int)maximumLevel;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where userIdArray.Contains(pba.UserId)
                      && cm.MixId == mixId && pba.MixId == mixId && cm.Level >= intMin && cm.Level <= intMax &&
                      c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb =>
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate, Judgements: JudgementsOf(pb)));
    }

    public async Task<RecordedPhoenixScore?> GetRecordedScore(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        return (await GetCachedScores(mix, userId, cancellationToken)).TryGetValue(chartId, out var r) ? r : null;
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetRecordedUserScores(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.MixId == mixId && pba.Score != null
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, u.IsPublic, pba.RecordedDate))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartScoreAggregate>> GetAllChartScoreAggregates(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        // The Plate column stores GetName() spellings ("Perfect Game", with the space) —
        // matching ToString() ("PerfectGame") counts zero PGs on every chart.
        var perfectGame = PhoenixPlate.PerfectGame.GetName();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
            where pba.Score != null && pba.MixId == mixId
            group pba by pba.ChartId
            into g
            select new ChartScoreAggregate(g.Key, g.Count(), g.Count(p => !p.IsBroken),
                g.Count(p => !p.IsBroken && p.Plate == perfectGame)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        var chartIdArray = chartIds.Distinct().ToArray();
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Cohort percentile machinery only — a walkoff in the distribution makes everyone
        // else's percentile look better than it is, so broken rows never enter.
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
                where chartIdArray.Contains(pba.ChartId) && pba.MixId == mixId && pba.Score != null &&
                      !pba.IsBroken &&
                      userIdArray.Contains(pba.UserId)
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, u.IsPublic, pba.RecordedDate))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetPlayerScoresInLevelRange(MixEnum mix,
        IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel minimumLevel,
        DifficultyLevel maximumLevel, CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        var mixId = MixIds.For(mix);
        var min = (int)minimumLevel;
        var max = (int)maximumLevel;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Same cohort-only contract as the other cohort reads: a broken row is a walkoff in
        // the distribution and never enters.
        return await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                join u in database.User on pba.UserId equals u.Id
                where cm.MixId == mixId && pba.MixId == mixId
                      && cm.Level >= min && cm.Level <= max && c.Type == chartTypeString
                      && pba.Score != null && !pba.IsBroken
                      && userIdArray.Contains(pba.UserId)
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value, PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, u.IsPublic,
                    pba.RecordedDate))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(
        MixEnum mix, IEnumerable<Guid> userIds, ChartType chartType, DifficultyLevel difficulty,
        CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.ToArray();
        var mixId = MixIds.For(mix);
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Same cohort-only contract as the chart-id overload above: percentile
        // distributions and competitive-neighbor reads never see broken rows.
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where
                    userIdArray.Contains(pba.UserId) && !pba.IsBroken &&
                    cm.MixId == mixId && pba.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate, Judgements: JudgementsOf(pb))));
    }


    public async Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetAllPlayerScores(MixEnum mix,
        ChartType chartType,
        DifficultyLevel difficulty, CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pba in database.Set<PhoenixRecordEntity>() on c.Id equals pba.ChartId
                where cm.MixId == mixId && pba.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                select pba).ToArrayAsync(cancellationToken))
            .Select(pb => (pb.UserId,
                new RecordedPhoenixScore(pb.ChartId, pb.Score, PhoenixPlateHelperMethods.TryParse(pb.Plate),
                    pb.IsBroken, pb.RecordedDate, Judgements: JudgementsOf(pb))));
    }

    public async Task<IEnumerable<ChartScoreAggregate>> GetMeaningfulScoresCount(MixEnum mix, ChartType chartType,
        DifficultyLevel difficulty,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        var intLevel = (int)difficulty;
        var chartTypeString = chartType.ToString();
        // Competitive-level cohort comes from PlayerProgress's published reader — its
        // PlayerStats table is vertical-internal, so no SQL join onto it from here.
        var cohort = (await _playerStats.GetPlayersByCompetitiveRange(mix, chartType, intLevel, .5, cancellationToken))
            .ToHashSet();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await (from cm in database.ChartMix
                join c in database.Chart on cm.ChartId equals c.Id
                join pr in database.Set<PhoenixRecordEntity>() on cm.ChartId equals pr.ChartId
                where cm.MixId == mixId && pr.MixId == mixId && cm.Level == intLevel && c.Type == chartTypeString
                      && cohort.Contains(pr.UserId)
                select pr).ToArrayAsync(cancellationToken))
            .GroupBy(c => c.ChartId).Select(g => new ChartScoreAggregate(g.Key, g.Count()));
    }

    public async Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(MixEnum mix, IEnumerable<Guid> userIds,
        Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var userIdArray = userIds.Distinct().ToArray();
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await (from pba in database.Set<PhoenixRecordEntity>()
                join u in database.User on pba.UserId equals u.Id
                where pba.ChartId == chartId && pba.MixId == mixId && pba.Score != null &&
                      userIdArray.Contains(pba.UserId)
                select new UserPhoenixScore(pba.UserId, pba.ChartId, u.IsPublic ? u.Name : "Anonymous",
                    pba.Score!.Value,
                    PhoenixPlateHelperMethods.TryParse(pba.Plate), pba.IsBroken, u.IsPublic, pba.RecordedDate))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> GetClearCount(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default)
    {
        var chartIds = (await _charts.GetCharts(mix, level, chartType, null, cancellationToken))
            .Select(c => c.Id).Distinct().ToHashSet();
        return (await GetCachedScores(mix, userId, cancellationToken)).Count(c =>
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
            // request.ChartMix filters the mix a chart DEBUTED in (OriginalMixId) — a
            // different semantic from the mix a record was scored under, below.
            var mixId = MixIds.For(request.ChartMix.Value);
            chartQuery = chartQuery.Where(c => c.OriginalMixId == mixId);
        }

        // request.Mix is the mix the records were scored under (contrast with ChartMix above).
        var recordMixId = MixIds.For(request.Mix);
        return await (from p in playerQuery
            join pr in database.Set<PhoenixRecordEntity>() on p.Id equals pr.UserId
            join c in chartQuery on pr.ChartId equals c.Id
            join prs in database.Set<PhoenixRecordStatsEntity>() on new { pr.ChartId, pr.UserId, pr.MixId } equals new
                { prs.ChartId, prs.UserId, prs.MixId }
            where pr.MixId == recordMixId
            group new { pr, prs } by pr.UserId
            into g
            select new UserChartAggregate(g.Key, g.Count(e => !e.pr.IsBroken), g.Count(),
                (int)g.Average(e => e.pr.Score ?? 0),
                g.Sum(e => e.prs.Pumbility), g.Sum(e => e.prs.PumbilityPlus))).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MixScoreCount>> GetMixesWithScores(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Both tables: Phoenix-scoring mixes record in PhoenixRecord, every pre-Phoenix mix in
        // BestAttempt. A mix with nothing in it still comes back — hiding the empties is what
        // made the picker look like it had forgotten the old mixes existed.
        var phoenix = await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId)
            .GroupBy(p => p.MixId)
            .Select(g => new { MixId = g.Key, Count = g.Count() })
            .ToArrayAsync(cancellationToken);
        var legacy = await database.Set<BestAttemptEntity>()
            .Where(b => b.UserId == userId)
            .GroupBy(b => b.MixId)
            .Select(g => new { MixId = g.Key, Count = g.Count() })
            .ToArrayAsync(cancellationToken);
        var counts = phoenix.Concat(legacy)
            .GroupBy(x => x.MixId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        // Ordered by the Mix table's SortOrder, never enum order.
        var mixes = await database.Set<MixEntity>()
            .OrderBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.IsPrimary })
            .ToArrayAsync(cancellationToken);
        return mixes
            .Where(m => MixIds.IsKnown(m.Id))
            .Select(m => new MixScoreCount(MixIds.ToEnum(m.Id), counts.GetValueOrDefault(m.Id), m.IsPrimary))
            .ToArray();
    }

    public async Task DeleteRecord(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId && p.ChartId == chartId && p.MixId == mixId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<PhoenixRecordStatsEntity>()
            .Where(p => p.UserId == userId && p.ChartId == chartId && p.MixId == mixId)
            .ExecuteDeleteAsync(cancellationToken);
        _cache.Remove(ScoreCache(userId, mix));
    }

    // Imported breaks only, and the same predicate on both the count and the delete so the number
    // on the button is the number that goes. Spelled out inline rather than shared through a
    // helper: EF has to translate it, so it cannot be a method call on the entity.
    public async Task<int> CountBrokenRecords(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<PhoenixRecordEntity>()
            .CountAsync(p => p.UserId == userId && p.MixId == mixId && p.IsBroken
                             && p.Source == ScoreJournalEntry.OfficialImportSource, cancellationToken);
    }

    public async Task<int> DeleteBrokenRecords(MixEnum mix, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The charts are read before the delete because the stats row is keyed by chart, not by
        // brokenness — once the records are gone there is nothing left to say which stats rows
        // belonged to them.
        var chartIds = await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId && p.MixId == mixId && p.IsBroken
                        && p.Source == ScoreJournalEntry.OfficialImportSource)
            .Select(p => p.ChartId)
            .ToArrayAsync(cancellationToken);
        if (chartIds.Length == 0) return 0;

        var removed = await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId && p.MixId == mixId && p.IsBroken
                        && p.Source == ScoreJournalEntry.OfficialImportSource)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<PhoenixRecordStatsEntity>()
            .Where(p => p.UserId == userId && p.MixId == mixId && chartIds.Contains(p.ChartId))
            .ExecuteDeleteAsync(cancellationToken);
        _cache.Remove(ScoreCache(userId, mix));
        return removed;
    }

    public async Task<IReadOnlyList<Guid>> GetUsersWithJudgedRecords(MixEnum mix,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<PhoenixRecordEntity>()
            .Where(p => p.MixId == mixId && p.Perfects != null)
            .Select(p => p.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task SetMaxCombos(MixEnum mix, Guid userId, IReadOnlyList<(Guid ChartId, int? MaxCombo)> combos,
        CancellationToken cancellationToken = default)
    {
        if (combos.Count == 0) return;

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId && p.MixId == mixId && p.Perfects != null)
            .ToArrayAsync(cancellationToken);
        var byChart = rows.ToDictionary(r => r.ChartId);
        foreach (var (chartId, maxCombo) in combos)
            if (byChart.TryGetValue(chartId, out var row))
                row.MaxCombo = maxCombo;

        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(ScoreCache(userId, mix));
    }

    public async Task DeleteAllForUser(Guid userId, MixEnum? mix = null,
        CancellationToken cancellationToken = default)
    {
        var mixId = mix == null ? (Guid?)null : MixIds.For(mix.Value);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<PhoenixRecordEntity>()
            .Where(p => p.UserId == userId && (mixId == null || p.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<PhoenixRecordStatsEntity>()
            .Where(p => p.UserId == userId && (mixId == null || p.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
        // Cheaper to drop every per-(user, mix) entry than to reason about which survived.
        foreach (var cached in Enum.GetValues<MixEnum>())
            _cache.Remove(ScoreCache(userId, cached));
    }
}
