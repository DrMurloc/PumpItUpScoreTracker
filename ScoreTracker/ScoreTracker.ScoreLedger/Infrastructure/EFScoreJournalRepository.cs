using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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
        // is never touched: it may already be a best, and an observation must not demote it.
        var known = (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && chartIds.Contains(e.ChartId) &&
                            occurred.Contains(e.OccurredAt))
                .Select(e => new { e.MixId, e.ChartId, e.OccurredAt })
                .ToArrayAsync(cancellationToken))
            .Select(e => (e.MixId, e.ChartId, e.OccurredAt))
            .ToHashSet();

        foreach (var entry in entries)
        {
            var mixId = MixIds.For(entry.Mix);
            if (!known.Add((mixId, entry.ChartId, entry.OccurredAt))) continue;

            await database.AddAsync(Entity(entry, mixId, false), cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static ScoreEventJournalEntity Entity(ScoreJournalEntry entry, Guid mixId, bool isBest)
    {
        return new ScoreEventJournalEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OccurredAt = entry.OccurredAt,
            Source = entry.Source,
            MixId = mixId,
            UserId = entry.UserId,
            ChartId = entry.ChartId,
            Score = entry.Score,
            Plate = entry.Plate?.GetName(),
            IsBroken = entry.IsBroken,
            IsBest = isBest,
            SessionId = entry.SessionId,
            Perfects = entry.Judgements?.Perfects,
            Greats = entry.Judgements?.Greats,
            Goods = entry.Judgements?.Goods,
            Bads = entry.Judgements?.Bads,
            Misses = entry.Judgements?.Misses
        };
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
        // Chart ids are mix-scoped by construction — no mix filter needed.
        var ids = chartIds.Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreEventJournalEntity>()
                .Where(e => e.UserId == userId && ids.Contains(e.ChartId))
                .OrderBy(e => e.OccurredAt)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
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

    private static ScoreJournalEntry Map(ScoreEventJournalEntity e)
    {
        return new ScoreJournalEntry(e.OccurredAt, e.Source, e.UserId, e.ChartId, e.Score,
            PhoenixPlateHelperMethods.TryParse(e.Plate), e.IsBroken, MixIds.ToEnum(e.MixId), e.SessionId,
            JudgementsOf(e), e.IsBest);
    }

    internal static JudgementCounts? JudgementsOf(ScoreEventJournalEntity e)
    {
        return e.Perfects == null
            ? null
            : new JudgementCounts(e.Perfects.Value, e.Greats!.Value, e.Goods!.Value, e.Bads!.Value, e.Misses!.Value);
    }
}
