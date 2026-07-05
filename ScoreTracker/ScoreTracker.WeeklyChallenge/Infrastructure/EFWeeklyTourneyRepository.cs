using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.WeeklyChallenge.Infrastructure
{
    internal sealed class EFWeeklyTourneyRepository
        (IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache) : IWeeklyTournamentRepository
    {
        public async Task<IEnumerable<Guid>> GetAlreadyPlayedCharts(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await database.Set<PastTourneyChartsEntity>().Where(e => e.MixId == mixId)
                .Select(e => e.ChartId).ToArrayAsync(cancellationToken);
        }

        public async Task ClearAlreadyPlayedCharts(MixEnum mix, IEnumerable<Guid> chartIds,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entities = await database.Set<PastTourneyChartsEntity>()
                .Where(e => chartIds.Contains(e.ChartId) && e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            database.Set<PastTourneyChartsEntity>().RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteAlreadyPlayedCharts(MixEnum mix, IEnumerable<Guid> chartIds,
            CancellationToken cancellationToken)
        {
            var alreadyPlayed = (await GetAlreadyPlayedCharts(mix, cancellationToken)).Distinct().ToHashSet();
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            await database.Set<PastTourneyChartsEntity>().AddRangeAsync(chartIds
                .Where(c => !alreadyPlayed.Contains(c)).Select(c => new PastTourneyChartsEntity
                {
                    ChartId = c,
                    MixId = mixId,
                    PlayedOn = DateTimeOffset.Now
                }), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteHistories(MixEnum mix, IEnumerable<UserTourneyHistory> histories,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            await database.Set<UserWeeklyPlacingEntity>().AddRangeAsync(histories.Select(h =>
                new UserWeeklyPlacingEntity
                {
                    ChartId = h.ChartId,
                    MixId = mixId,
                    IsBroken = h.IsBroken,
                    ObtainedDate = h.ReceivedOn,
                    Plate = h.Plate.ToString(),
                    Place = h.Place,
                    Score = h.Score,
                    UserId = h.UserId,
                    CompetitiveLevel = h.CompetitiveLevel
                }), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task ClearTheBoard(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var userEntries = await database.Set<WeeklyUserEntry>().Where(e => e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            var weeklyCharts = await database.Set<WeeklyTournamentChartEntity>().Where(e => e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            database.Set<WeeklyUserEntry>().RemoveRange(userEntries);
            database.Set<WeeklyTournamentChartEntity>().RemoveRange(weeklyCharts);
            await database.SaveChangesAsync(cancellationToken);
            cache.Remove(WeeklyChartsKey(mix));
        }

        public async Task RegisterWeeklyChart(MixEnum mix, WeeklyTournamentChart chart,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            await database.Set<WeeklyTournamentChartEntity>().AddAsync(new WeeklyTournamentChartEntity
            {
                ChartId = chart.ChartId,
                MixId = MixIds.For(mix),
                ExpirationDate = chart.ExpirationDate
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            cache.Remove(WeeklyChartsKey(mix));
        }

        private static string WeeklyChartsKey(MixEnum mix)
        {
            return $@"{nameof(EFWeeklyTourneyRepository)}__WeeklyCharts__{mix}";
        }

        public async Task<IEnumerable<WeeklyTournamentChart>> GetWeeklyCharts(MixEnum mix,
            CancellationToken cancellationToken)
        {
            return await cache.GetOrCreateAsync(WeeklyChartsKey(mix), async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                await using var database = await factory.CreateDbContextAsync(cancellationToken);
                var mixId = MixIds.For(mix);
                return await database.Set<WeeklyTournamentChartEntity>().Where(w => w.MixId == mixId).Select(w =>
                    new WeeklyTournamentChart(w.ChartId, w.ExpirationDate)).ToArrayAsync(cancellationToken);
            });
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> GetEntries(MixEnum mix, Guid? chartId,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var query = database.Set<WeeklyUserEntry>().Where(w => w.MixId == mixId);
            if (chartId != null) query = query.Where(w => w.ChartId == chartId);

            return (await query.ToArrayAsync(cancellationToken)).Select(q => new WeeklyTournamentEntry(q.UserId,
                q.ChartId, q.Score, Enum.Parse<PhoenixPlate>(q.Plate), q.IsBroken,
                q.Photo == null ? null : new Uri(q.Photo, UriKind.Absolute), q.CompetitiveLevel));
        }

        public async Task SaveEntry(MixEnum mix, WeeklyTournamentEntry entry, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await
                database.Set<WeeklyUserEntry>().FirstOrDefaultAsync(
                    e => e.UserId == entry.UserId && e.ChartId == entry.ChartId && e.MixId == mixId,
                    cancellationToken);
            if (entity == null)
            {
                await database.Set<WeeklyUserEntry>().AddAsync(new WeeklyUserEntry
                {
                    ChartId = entry.ChartId,
                    MixId = mixId,
                    IsBroken = entry.IsBroken,
                    Plate = entry.Plate.ToString(),
                    Score = entry.Score,
                    UserId = entry.UserId,
                    Photo = entry.PhotoUrl?.ToString(),
                    CompetitiveLevel = entry.CompetitiveLevel
                }, cancellationToken);
            }
            else
            {
                entity.Plate = entry.Plate.ToString();
                entity.Score = entry.Score;
                entity.CompetitiveLevel = entry.CompetitiveLevel;
                entity.IsBroken = entry.IsBroken;
                entity.Photo = entry.PhotoUrl?.ToString();
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<DateTimeOffset>> GetPastDates(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await database.Set<UserWeeklyPlacingEntity>().Where(u => u.MixId == mixId)
                .Select(u => u.ObtainedDate).Distinct()
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> GetPastEntries(MixEnum mix, DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return (await database.Set<UserWeeklyPlacingEntity>()
                .Where(e => e.ObtainedDate == date && e.MixId == mixId).ToArrayAsync(cancellationToken)).Select(u =>
                new WeeklyTournamentEntry(u.UserId, u.ChartId, u.Score, Enum.Parse<PhoenixPlate>(u.Plate), u.IsBroken,
                    null, u.CompetitiveLevel));
        }
    }
}
