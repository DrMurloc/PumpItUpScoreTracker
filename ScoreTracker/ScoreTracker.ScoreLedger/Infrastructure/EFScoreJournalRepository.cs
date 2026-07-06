using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
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
        await database.Set<ScoreEventJournalEntity>().AddAsync(new ScoreEventJournalEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            OccurredAt = entry.OccurredAt,
            Source = entry.Source,
            MixId = MixIds.For(entry.Mix),
            UserId = entry.UserId,
            ChartId = entry.ChartId,
            Score = entry.Score,
            Plate = entry.Plate?.GetName(),
            IsBroken = entry.IsBroken,
            SessionId = entry.SessionId
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int TotalGroups, IReadOnlyList<JournalSessionRows> Groups)> GetSessionGroups(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken)
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

    private static ScoreJournalEntry Map(ScoreEventJournalEntity e)
    {
        return new ScoreJournalEntry(e.OccurredAt, e.Source, e.UserId, e.ChartId, e.Score,
            PhoenixPlateHelperMethods.TryParse(e.Plate), e.IsBroken, MixIds.ToEnum(e.MixId), e.SessionId);
    }
}
