using MediatR;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Application.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFPlayerStatsRepository : IPlayerStatsRepository,
        IRequestHandler<GetPlayerStatsQuery, PlayerStatsRecord>
    {
        private readonly ChartAttemptDbContext _database;

        public EFPlayerStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _database = factory.CreateDbContext();
        }

        public async Task SaveStats(Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken)
        {
            var entity = await _database.PlayerStats.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (entity == null)
            {
                await _database.AddAsync(new PlayerStatsEntity
                {
                    UserId = userId,
                    CoOpRating = newStats.CoOpRating,
                    SinglesRating = newStats.SinglesRating,
                    DoublesRating = newStats.DoublesRating,
                    SkillRating = newStats.SkillRating,
                    TotalRating = newStats.TotalRating,
                    AverageCoOpScore = newStats.CoOpScore,
                    AverageDoublesLevel = newStats.DoublesLevel,
                    AverageDoublesScore = newStats.DoublesScore,
                    AverageSinglesLevel = newStats.SinglesLevel,
                    AverageSinglesScore = newStats.SinglesScore,
                    AverageSkillLevel = newStats.SkillLevel,
                    AverageSkillScore = newStats.SkillScore,
                    HighestLevel = newStats.HighestLevel,
                    ClearCount = newStats.ClearCount
                }, cancellationToken);
            }
            else
            {
                entity.CoOpRating = newStats.CoOpRating;
                entity.SinglesRating = newStats.SinglesRating;
                entity.DoublesRating = newStats.DoublesRating;
                entity.SkillRating = newStats.SkillRating;
                entity.TotalRating = newStats.TotalRating;
                entity.AverageCoOpScore = newStats.CoOpScore;
                entity.AverageDoublesLevel = newStats.DoublesLevel;
                entity.AverageDoublesScore = newStats.DoublesScore;
                entity.AverageSinglesLevel = newStats.SinglesLevel;
                entity.AverageSinglesScore = newStats.SinglesScore;
                entity.AverageSkillLevel = newStats.SkillLevel;
                entity.AverageSkillScore = newStats.SkillScore;
                entity.HighestLevel = newStats.HighestLevel;
                entity.ClearCount = newStats.ClearCount;
            }

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task<PlayerStatsRecord> Handle(GetPlayerStatsQuery request, CancellationToken cancellationToken)
        {
            var entity =
                await _database.PlayerStats.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
            if (entity == null) return new PlayerStatsRecord(0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            return new PlayerStatsRecord(entity.TotalRating, entity.HighestLevel, entity.ClearCount, entity.CoOpRating,
                entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                entity.SinglesRating,
                entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                entity.AverageDoublesScore, entity.AverageDoublesLevel);
        }
    }
}
