using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.Domain.Enums;
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
            // All live submissions are Phoenix-mix today; recorded per row so the journal
            // stays honest when Phoenix 2 arrives.
            MixId = MixIds.Phoenix,
            UserId = entry.UserId,
            ChartId = entry.ChartId,
            Score = entry.Score,
            Plate = entry.Plate?.GetName(),
            IsBroken = entry.IsBroken
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }
}
