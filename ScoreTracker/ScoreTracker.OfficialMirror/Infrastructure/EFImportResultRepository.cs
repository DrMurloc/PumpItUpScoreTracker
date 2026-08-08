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

    public async Task Close(Guid id, DateTimeOffset finishedAt, ImportOutcome outcome,
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
                .SetProperty(r => r.Outcome, outcome.ToString()), cancellationToken);
    }

    public async Task AttachSession(Guid id, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ImportResultEntity>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.SessionId, sessionId), cancellationToken);
    }
}
