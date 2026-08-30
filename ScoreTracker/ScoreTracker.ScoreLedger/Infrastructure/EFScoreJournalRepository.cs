using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFScoreJournalRepository : IScoreJournalRepository
{

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFScoreJournalRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Append(ScoreJournalEntry entry, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(entry.Mix);
        // One import can see the same play twice — once in recently-played as an observation,
        // once on the best list as the record change — and both carry the site's play time.
        // Raising the existing row is what makes them one play instead of two.
        var existing = await database.Set<ScoreEventJournalEntity>().FirstOrDefaultAsync(
            e => e.UserId == entry.UserId && e.MixId == mixId && e.ChartId == entry.ChartId &&
                 e.OccurredAt == entry.OccurredAt, cancellationToken);
        if (existing != null)
        {
            // Same key, different result: these are two plays, and the timestamp cannot tell them
            // apart because it did not come from a play at all — a best-list card is stamped when
            // the chart first reaches the list and keeps that stamp as the score improves. Raising
            // the flag here would leave one play's row wearing another play's standing (the shape
            // that put a broken score under a passing record). The row is left exactly as it is:
            // what it says happened, happened. The record itself is written either way, and the
            // play earns its own row as soon as a recent window dates it.
            if (!IsSamePlay(existing, entry)) return;

            existing.IsBest = true;
            existing.SessionId ??= entry.SessionId;
            await database.SaveChangesAsync(cancellationToken);
            return;
        }

        await database.AddAsync(Entity(entry, mixId, true), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendObservations(IReadOnlyList<ScoreJournalEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return;

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var userId = entries[0].UserId;
        var chartIds = entries.Select(e => e.ChartId).Distinct().ToArray();
        var occurred = entries.Select(e => e.OccurredAt).Distinct().ToArray();
        // One read of the candidate window, then insert only what isn't there. An existing row
        // is never demoted: it may already be a best. The one thing an observation may add to a
        // row already there is a judgement breakdown the row lacks — the same play reaches us
        // twice when the best list keeps a stage break as a chart's first attempt (no breakdown)
        // and the recent window still holds the play (with one), and the judged twin is the
        // one worth keeping whichever order they arrive in.
        var known = (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && chartIds.Contains(e.ChartId) &&
                            occurred.Contains(e.OccurredAt))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(e => (e.MixId, e.ChartId, e.OccurredAt));

        // Judged entries first, so that within one batch the twin that carries a breakdown is
        // the one that gets inserted and the unjudged one is the duplicate.
        foreach (var entry in entries.OrderByDescending(e => e.Judgements != null))
        {
            var mixId = MixIds.For(entry.Mix);
            var key = (mixId, entry.ChartId, entry.OccurredAt);
            if (known.TryGetValue(key, out var existing))
            {
                // A best-list card gives us a stage break with no breakdown; the recently-played
                // card in the same import fills it in. The cause is solved from that breakdown,
                // so it lands with it or not at all. Only onto the SAME KIND of play: a Phoenix 2
                // best card is stamped at the chart's first attempt and keeps that stamp, so a
                // judged stage break can share a key with an unjudged passing best — two
                // different plays — and filling across that line would stamp a pass with a stage
                // break's partial counts and cause.
                if (existing.Perfects == null && entry.Judgements != null
                    && existing.IsStageBroken == entry.IsStageBroken)
                {
                    SetJudgements(existing, entry.Judgements);
                    SetCause(existing, entry.Cause);
                }

                continue;
            }

            var entity = Entity(entry, mixId, false);
            known.Add(key, entity);
            await database.AddAsync(entity, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Whether a stored row and an incoming entry are the same play rather than two plays
    ///     sharing a fabricated timestamp. Result, not metadata: an import sees one play twice —
    ///     as a recently-played observation and as the best-list card that produced it — and those
    ///     two agree on every axis below, which is what makes them collapse onto one row.
    /// </summary>
    private static bool IsSamePlay(ScoreEventJournalEntity existing, ScoreJournalEntry entry)
    {
        var score = entry.Score != null ? (int?)entry.Score.Value
            : entry.LegacyScore != null ? (int?)entry.LegacyScore.Value : null;
        return existing.Score == score
               && existing.IsBroken == entry.IsBroken
               && existing.IsStageBroken == entry.IsStageBroken;
    }

    private static void SetJudgements(ScoreEventJournalEntity entity, JudgementCounts? judgements)
    {
        entity.Perfects = judgements?.Perfects;
        entity.Greats = judgements?.Greats;
        entity.Goods = judgements?.Goods;
        entity.Bads = judgements?.Bads;
        entity.Misses = judgements?.Misses;
        entity.MaxCombo = judgements?.MaxCombo;
    }

    /// <summary>
    ///     Names are stored rather than ordinals, matching Plate and LetterGrade beside them: the
    ///     column is readable in an ad-hoc query, and a reordered enum cannot silently repoint
    ///     existing rows at a different plate.
    /// </summary>
    private static void SetCause(ScoreEventJournalEntity entity, StageBreakCause cause)
    {
        entity.IsNonLifebarBreak = cause.IsNonLifebarBreak;
        entity.PassPlate = cause.PassPlate?.GetName();
        entity.PassGrade = cause.PassGrade?.GetName();
    }

    private static ScoreEventJournalEntity Entity(ScoreJournalEntry entry, Guid mixId, bool isBest)
    {
        var entity = new ScoreEventJournalEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OccurredAt = entry.OccurredAt,
            Source = entry.Source,
            MixId = mixId,
            UserId = entry.UserId,
            ChartId = entry.ChartId,
            // One column per axis, fed from whichever scoring model this entry belongs to.
            // A legacy number is an int rather than a PhoenixScore precisely because it can
            // exceed 1,000,000; the column has always been a plain int and holds it fine.
            Score = entry.Score != null ? (int?)entry.Score.Value
                : entry.LegacyScore != null ? (int?)entry.LegacyScore.Value : null,
            Plate = entry.Plate?.GetName(),
            LetterGrade = entry.LegacyGrade?.ToString(),
            IsBroken = entry.IsBroken,
            // A stage break is never a best, whatever the caller said: the flag wins.
            IsStageBroken = entry.IsStageBroken,
            IsBest = isBest && !entry.IsStageBroken,
            SessionId = entry.SessionId
        };
        SetJudgements(entity, entry.Judgements);
        SetCause(entity, entry.Cause);
        return entity;
    }

    public async Task<(int TotalGroups, IReadOnlyList<JournalSessionRows> Groups)> GetSessionGroups(
        Guid userId, int page, int pageSize, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = database.Set<ScoreEventJournalEntity>().Where(e => e.UserId == userId);

        // Group keys are small (one per session / per pre-capture mix-day), so paging
        // happens in memory over the keys and only the paged groups' rows are loaded.
        // A session belongs to exactly one mix by construction (the batcher keys
        // envelopes per (user, mix, source)).
        var sessionKeys = await rows.Where(e => e.SessionId != null)
            .GroupBy(e => e.SessionId)
            .Select(g => new { SessionId = g.Key, MixId = g.Max(e => e.MixId), Latest = g.Max(e => e.OccurredAt) })
            .ToArrayAsync(cancellationToken);
        var dayKeys = await rows.Where(e => e.SessionId == null)
            .GroupBy(e => new { e.MixId, e.OccurredAt.Date })
            .Select(g => new { g.Key.MixId, Day = g.Key.Date, Latest = g.Max(e => e.OccurredAt) })
            .ToArrayAsync(cancellationToken);

        var ordered = sessionKeys.Select(k => (k.SessionId, Day: (DateTime?)null, k.MixId, k.Latest))
            .Concat(dayKeys.Select(k => (SessionId: (Guid?)null, Day: (DateTime?)k.Day, k.MixId, k.Latest)))
            .Where(k => before == null || k.Latest < before)
            .OrderByDescending(k => k.Latest)
            .ToArray();
        var pageKeys = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        if (pageKeys.Length == 0) return (ordered.Length, Array.Empty<JournalSessionRows>());

        var sessionIds = pageKeys.Where(k => k.SessionId != null).Select(k => k.SessionId).ToArray();
        // Day buckets load by date (a superset across mixes) and split per mix below.
        var days = pageKeys.Where(k => k.Day != null).Select(k => k.Day!.Value).ToArray();
        var pageRows = (await rows.Where(e =>
                    (e.SessionId != null && sessionIds.Contains(e.SessionId)) ||
                    (e.SessionId == null && days.Contains(e.OccurredAt.Date)))
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();

        var groups = pageKeys.Select(k => new JournalSessionRows(
                k.SessionId,
                k.Day == null ? null : DateOnly.FromDateTime(k.Day.Value),
                MixIds.ToEnum(k.MixId),
                pageRows.Where(r => k.SessionId != null
                        ? r.SessionId == k.SessionId
                        : r.SessionId == null && r.OccurredAt.Date == k.Day!.Value
                                              && MixIds.For(r.Mix) == k.MixId)
                    .ToArray()))
            .ToArray();
        return (ordered.Length, groups);
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetChartHistories(Guid userId,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
    {
        // ⚠ Deliberately CROSS-MIX, and callers must know it. A returning song carries one
        // ChartId across Phoenix and Phoenix 2, so this returns both mixes' plays for such a
        // chart — which is exactly what reclear detection needs, and exactly what a replay
        // rebuilding one mix's record must filter out first. (This comment used to claim chart
        // ids were mix-scoped. They are not, and the undo replay trusted that.)
        var ids = chartIds.Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && ids.Contains(e.ChartId))
                .OrderBy(e => e.OccurredAt)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetJournalPage(Guid userId, MixEnum mix,
        DateTimeOffset? beforeOccurredAt, Guid? beforeChartId, DateTimeOffset? since, int limit,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        var query = database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId);

        if (since is not null) query = query.Where(e => e.OccurredAt >= since.Value);

        // The compound keyset: strictly older, or the same instant and a lower chart id. Written as
        // one predicate rather than a tuple comparison because the provider translates this form.
        if (beforeOccurredAt is not null)
            query = beforeChartId is null
                ? query.Where(e => e.OccurredAt < beforeOccurredAt.Value)
                : query.Where(e => e.OccurredAt < beforeOccurredAt.Value
                                   || (e.OccurredAt == beforeOccurredAt.Value && e.ChartId < beforeChartId.Value));

        return (await query
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.ChartId)
                .Take(limit)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetJudgedPlays(Guid userId, MixEnum mix, int limit,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Judgement-carrying complete screens only: the five counts arrive together, so one
        // non-null column implies the set, and a stage break's counts stop mid-chart.
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.Perfects != null && !e.IsStageBroken)
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.ChartId)
                .Take(limit)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyList<UserPhoenixScore>> GetLowestPassingPlays(MixEnum mix, Guid chartId,
        int limit, CancellationToken cancellationToken)
    {
        // The board's row type is a UserPhoenixScore, whose score caps at 1,000,000 — and a
        // legacy journal row's Score is an era score, three quarters of which are above that.
        // Empty rather than a throw: nothing returns rows for a legacy mix today (LimboChart
        // rows are inserted by hand and only for Phoenix charts), but the chart page passes the
        // chart's own mix, so one hand-run INSERT on a legacy chart is all it would take.
        if (mix.UsesLegacyScoring()) return Array.Empty<UserPhoenixScore>();

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Grouped in SQL and capped there: the aggregate is served entirely out of
        // IX_ScoreEventJournal_ChartId_MixId, so nothing walks the journal and nothing comes back
        // that the board will not draw.
        //
        // The tie break is the player's FIRST observed clear of the chart, not the timestamp of the
        // low run itself — picking the producing row's date needs a per-group correlated aggregate
        // that does not translate, and "who has been at this longest" breaks a tie just as fairly.
        //
        // No plate: the column is nvarchar(max) so it cannot ride the index without a key lookup
        // per row, and a limbo pass is a Rough Game by construction. The letter grade — the part
        // that actually reads on the board — is derived from the score by ScoreBreakdown.
        var rows = await (from j in database.Set<ScoreEventJournalEntity>()
                join u in database.User on j.UserId equals u.Id
                where j.ChartId == chartId && j.MixId == mixId && !j.IsBroken && j.Score != null
                      && u.IsPublic
                group new { j.Score, j.OccurredAt } by new { j.UserId, u.Name }
                into g
                select new
                {
                    g.Key.UserId,
                    g.Key.Name,
                    Lowest = g.Min(x => x.Score!.Value),
                    FirstClear = g.Min(x => x.OccurredAt)
                })
            .OrderBy(r => r.Lowest)
            .ThenBy(r => r.FirstClear)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(r => new UserPhoenixScore(r.UserId, chartId, r.Name, r.Lowest, null, false, true,
                r.FirstClear))
            .ToArray();
    }

    public async Task DeleteForUser(Guid userId, MixEnum? mix, CancellationToken cancellationToken)
    {
        var mixId = mix == null ? (Guid?)null : MixIds.For(mix.Value);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && (mixId == null || e.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetChartPlayCounts(Guid userId, MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId)
            .GroupBy(e => e.ChartId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetSessionEntries(Guid userId, Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.SessionId == sessionId)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task DeleteSession(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetUsersWithJudgedEntries(MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.MixId == mixId && e.Perfects != null)
            .Select(e => e.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetJudgedEntries(Guid userId, MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.Perfects != null)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task SetMaxCombos(Guid userId, MixEnum mix,
        IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, int? MaxCombo)> combos,
        CancellationToken cancellationToken)
    {
        if (combos.Count == 0) return;

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId && e.Perfects != null)
            .ToArrayAsync(cancellationToken);
        var byKey = rows.ToDictionary(r => (r.ChartId, r.OccurredAt));
        foreach (var (chartId, occurredAt, maxCombo) in combos)
            if (byKey.TryGetValue((chartId, occurredAt), out var row))
                row.MaxCombo = maxCombo;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> GetJudgedStageBreaks(Guid userId, MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.Perfects != null && e.IsStageBroken)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task SetStageBreakCauses(Guid userId, MixEnum mix,
        IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)> causes,
        CancellationToken cancellationToken)
    {
        if (causes.Count == 0) return;

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ScoreEventJournalEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId && e.Perfects != null && e.IsStageBroken)
            .ToArrayAsync(cancellationToken);
        var byKey = rows.ToDictionary(r => (r.ChartId, r.OccurredAt));
        foreach (var (chartId, occurredAt, cause) in causes)
            if (byKey.TryGetValue((chartId, occurredAt), out var row))
                SetCause(row, cause);

        await database.SaveChangesAsync(cancellationToken);
    }

    private static ScoreJournalEntry Map(ScoreEventJournalEntity e)
    {
        var mix = MixIds.ToEnum(e.MixId);
        // The mix decides which side the stored number is. Reading a legacy row's score as a
        // PhoenixScore would throw on most of them -- 76% of the scored ones are above the
        // 1,000,000 ceiling.
        var isLegacy = mix.UsesLegacyScoring();
        return new ScoreJournalEntry(e.OccurredAt, e.Source, e.UserId, e.ChartId,
            isLegacy ? null : e.Score,
            isLegacy ? null : PhoenixPlateHelperMethods.TryParse(e.Plate),
            e.IsBroken, mix, e.SessionId, JudgementsOf(e), e.IsBest,
            isLegacy && e.Score != null ? (XXScore?)e.Score.Value : null,
            Enum.TryParse<XXLetterGrade>(e.LetterGrade, out var grade) ? grade : null,
            e.IsStageBroken,
            new StageBreakCause(e.IsNonLifebarBreak, PhoenixPlateHelperMethods.TryParse(e.PassPlate),
                PhoenixLetterGradeHelperMethods.TryParse(e.PassGrade)));
    }

    internal static JudgementCounts? JudgementsOf(ScoreEventJournalEntity e)
    {
        return e.Perfects == null
            ? null
            : new JudgementCounts(e.Perfects.Value, e.Greats!.Value, e.Goods!.Value, e.Bads!.Value, e.Misses!.Value,
                e.MaxCombo);
    }
}
