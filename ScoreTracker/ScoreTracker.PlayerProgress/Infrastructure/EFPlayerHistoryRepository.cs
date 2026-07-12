using MediatR;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.PlayerProgress.Infrastructure
{
    internal sealed class EFPlayerHistoryRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        : IPlayerHistoryRepository,
            IRequestHandler<GetPlayerHistoryQuery, IEnumerable<PlayerRatingRecord>>
    {
        public async Task WriteHistory(MixEnum mix, PlayerRatingRecord record, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            await database.Set<PlayerHistoryEntity>().AddAsync(new PlayerHistoryEntity
            {
                UserId = record.UserId,
                MixId = MixIds.For(mix),
                CoOpRating = record.CoOpRating,
                Date = record.Date,
                CompetitiveLevel = record.CompetitiveLevel,
                SinglesLevel = record.SinglesLevel,
                DoublesLevel = record.DoublesLevel,
                PassCount = record.PassCount,
                SkillRating = record.SkillRating
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }


        public async Task<IEnumerable<PlayerRatingRecord>> Handle(GetPlayerHistoryQuery request,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(request.Mix);
            return await database.Set<PlayerHistoryEntity>()
                .Where(r => r.UserId == request.UserId && r.MixId == mixId)
                .Select(r => new PlayerRatingRecord(r.UserId, r.Date, r.CompetitiveLevel, r.SinglesLevel,
                    r.DoublesLevel, r.CoOpRating, r.PassCount, r.SkillRating)).ToArrayAsync(cancellationToken);
        }

        public async Task DeleteHistoryForUser(Guid userId, CancellationToken cancellationToken)
        {
            // Account-level wipe: spans every mix by design.
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var entries = await database.Set<PlayerHistoryEntity>().Where(r => r.UserId == userId)
                .ToArrayAsync(cancellationToken);
            database.Set<PlayerHistoryEntity>().RemoveRange(entries);
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
