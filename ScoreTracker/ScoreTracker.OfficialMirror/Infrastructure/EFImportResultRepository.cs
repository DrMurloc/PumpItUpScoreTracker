using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFImportResultRepository : IImportResultRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFImportResultRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Guid> Open(Guid userId, MixEnum mix, ImportKind kind, string? cardId,
        DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var id = Guid.NewGuid();
        database.Set<ImportResultEntity>().Add(new ImportResultEntity
        {
            Id = id,
            UserId = userId,
            MixId = MixIds.For(mix),
            Kind = kind.ToString(),
            CardId = string.IsNullOrWhiteSpace(cardId) ? null : cardId,
            StartedAt = startedAt
        });
        await database.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task Close(Guid id, DateTimeOffset finishedAt, ImportOutcome outcome, int? scoreCount,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Only ever closes an OPEN row. A second Close would be a run reporting two endings,
        // and the first one is the true one — whatever happened after it is noise from a
        // teardown path, not a revised verdict.
        await database.Set<ImportResultEntity>()
            .Where(r => r.Id == id && r.FinishedAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.FinishedAt, finishedAt)
                .SetProperty(r => r.Outcome, outcome.ToString())
                .SetProperty(r => r.ScoreCount, scoreCount), cancellationToken);
    }

    public async Task AttachSession(Guid id, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ImportResultEntity>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.SessionId, sessionId), cancellationToken);
    }

    public async Task<IReadOnlyList<ImportRunForSession>> GetForSessions(IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0) return Array.Empty<ImportRunForSession>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ImportResultEntity>()
            .Where(r => r.SessionId != null && sessionIds.Contains(r.SessionId.Value))
            .Select(r => new ImportRunForSession(r.Id, r.SessionId!.Value, r.StartedAt, r.FinishedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task MarkInterrupted(Guid id, DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Same rule as Close: a run that already reported an ending keeps it. This one is a
        // verdict reached from the outside, and it must never overwrite the run's own.
        await database.Set<ImportResultEntity>()
            .Where(r => r.Id == id && r.FinishedAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.FinishedAt, finishedAt)
                .SetProperty(r => r.Outcome, ImportOutcome.Interrupted.ToString()), cancellationToken);
    }

    public async Task<ImportAttemptRecord?> GetUnacknowledgedInterrupted(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The player's LATEST run, then a check on what it is — not "their latest interrupted
        // run". The notice tells somebody to import again, so any run after the interrupted one
        // makes it stale advice: they already did, and being told otherwise reads as the site
        // having lost track. Marking Interrupted happens at the next boot, so a player who
        // imports again before that boot lands exactly here.
        var row = await database.Set<ImportResultEntity>()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null || row.AcknowledgedAt != null) return null;
        return row.Outcome == ImportOutcome.Interrupted.ToString() ? Map(row) : null;
    }

    public async Task Acknowledge(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ImportResultEntity>()
            .Where(r => r.Id == id && r.AcknowledgedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.AcknowledgedAt, at), cancellationToken);
    }

    public async Task<IReadOnlyList<ImportAttemptRecord>> GetRecent(Guid userId, int take,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<ImportResultEntity>()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    private static ImportAttemptRecord Map(ImportResultEntity r)
    {
        return new ImportAttemptRecord(
            r.Id,
            MixIds.ToEnum(r.MixId),
            Enum.TryParse<ImportKind>(r.Kind, out var kind) ? kind : ImportKind.Standard,
            r.StartedAt,
            r.FinishedAt,
            // An unparseable outcome reads as "never reported back" rather than inventing a
            // verdict — the honest answer when the stored word is not one we know.
            Enum.TryParse<ImportOutcome>(r.Outcome, out var outcome) ? outcome : null,
            r.SessionId,
            r.ScoreCount);
    }
}
