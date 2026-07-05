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
}
