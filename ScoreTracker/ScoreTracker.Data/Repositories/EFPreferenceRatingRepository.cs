using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFPreferenceRatingRepository : IChartPreferenceRepository
    {
        private readonly ChartAttemptDbContext _database;

        //Will  need to refactor this if I ever support non prod environments
        //Mostly saving some tedious joins for now.
        private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
        {
            { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
            { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
        };

        public EFPreferenceRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _database = factory.CreateDbContext();
        }

        public async Task SaveRating(MixEnum mix, Guid userId, Guid chartId, Rating rating,
            CancellationToken cancellationToken)
        {
            var mixId = MixGuids[mix];
            var entity = await _database.UserPreferenceRating
                .Where(e => e.MixId == mixId && e.ChartId == chartId && e.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
                await _database.UserPreferenceRating.AddAsync(new UserPreferenceRatingEntity
                {
                    Id = Guid.NewGuid(),
                    MixId = mixId,
                    UserId = userId,
                    ChartId = chartId,
                    Rating = rating
                }, cancellationToken);
            else
                entity.Rating = rating;

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task SetAverageRating(MixEnum mix, Guid chartId, Rating averageRating, int ratingCount,
            CancellationToken cancellationToken)
        {
            var mixId = MixGuids[mix];
            var entity = await _database.ChartPreferenceRating.Where(c => c.MixId == mixId && c.ChartId == chartId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await _database.ChartPreferenceRating.AddAsync(new ChartPreferenceRatingEntity
                {
                    Id = Guid.NewGuid(),
                    MixId = mixId,
                    ChartId = chartId,
                    Rating = averageRating,
                    Count = ratingCount
                }, cancellationToken);
            }
            else
            {
                entity.Rating = averageRating;
                entity.Count = ratingCount;
            }

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<ChartPreferenceRatingRecord>> GetPreferenceRatings(MixEnum mix,
            CancellationToken cancellationToken)
        {
            var mixId = MixGuids[mix];
            return await _database.ChartPreferenceRating.Where(c => c.MixId == mixId).Select(cpr =>
                    new ChartPreferenceRatingRecord(cpr.ChartId, cpr.Rating, cpr.Count))
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<Rating>> GetRatingsForChart(MixEnum mix, Guid chartId,
            CancellationToken cancellationToken)
        {
            var mixId = MixGuids[mix];
            return (await _database.UserPreferenceRating.Where(e => e.MixId == mixId && e.ChartId == chartId)
                .ToArrayAsync(cancellationToken)).Select(e => Rating.From(e.Rating)).ToArray();
        }

        public async Task<IEnumerable<UserRatingsRecord>> GetUserRatings(MixEnum mix, Guid userId,
            CancellationToken cancellationToken)
        {
            var mixId = MixGuids[mix];
            return (await _database.UserPreferenceRating.Where(e => e.MixId == mixId && e.UserId == userId)
                    .ToArrayAsync(cancellationToken))
                .Select(u => new UserRatingsRecord(u.ChartId, u.Rating))
                .ToArray();
        }
    }
}
