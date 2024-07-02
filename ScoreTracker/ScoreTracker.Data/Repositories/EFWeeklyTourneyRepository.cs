using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFWeeklyTourneyRepository
        (ChartAttemptDbContext database, IMemoryCache cache) : IWeeklyTournamentRepository
    {
        public async Task<IEnumerable<Guid>> GetAlreadyPlayedCharts(CancellationToken cancellationToken)
        {
            return await database.PastTourneyCharts.Select(e => e.ChartId).ToArrayAsync(cancellationToken);
        }

        public async Task ClearAlreadyPlayedCharts(IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
        {
            var entities = await database.PastTourneyCharts.Where(e => chartIds.Contains(e.ChartId))
                .ToArrayAsync(cancellationToken);
            database.PastTourneyCharts.RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteAlreadyPlayedCharts(IEnumerable<Guid> chartIds, CancellationToken cancellationToken)
        {
            var alreadyPlayed = (await GetAlreadyPlayedCharts(cancellationToken)).Distinct().ToHashSet();
            await database.PastTourneyCharts.AddRangeAsync(chartIds
                .Where(c => !alreadyPlayed.Contains(c)).Select(c => new PastTourneyChartsEntity
                {
                    ChartId = c,
                    PlayedOn = DateTimeOffset.Now
                }), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteHistories(IEnumerable<UserTourneyHistory> histories, CancellationToken cancellationToken)
        {
            await database.UserWeeklyPlacing.AddRangeAsync(histories.Select(h => new UserWeeklyPlacingEntity
            {
                ChartId = h.ChartId,
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

        public async Task ClearTheBoard(CancellationToken cancellationToken)
        {
            var userEntries = await database.WeeklyUserEntry.ToArrayAsync(cancellationToken);
            var weeklyCharts = await database.WeeklyTournamentChart.ToArrayAsync(cancellationToken);
            database.WeeklyUserEntry.RemoveRange(userEntries);
            database.WeeklyTournamentChart.RemoveRange(weeklyCharts);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task RegisterWeeklyChart(WeeklyTournamentChart chart, CancellationToken cancellationToken)
        {
            await database.WeeklyTournamentChart.AddAsync(new WeeklyTournamentChartEntity
            {
                ChartId = chart.ChartId,
                ExpirationDate = chart.ExpirationDate
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            cache.Remove(WeeklyChartsKey);
        }

        private const string WeeklyChartsKey = $@"{nameof(EFWeeklyTourneyRepository)}__WeeklyCharts";

        public async Task<IEnumerable<WeeklyTournamentChart>> GetWeeklyCharts(CancellationToken cancellationToken)
        {
            return await cache.GetOrCreateAsync(WeeklyChartsKey, async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return await database.WeeklyTournamentChart.Select(w =>
                    new WeeklyTournamentChart(w.ChartId, w.ExpirationDate)).ToArrayAsync(cancellationToken);
            });
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> GetEntries(Guid? chartId,
            CancellationToken cancellationToken)
        {
            var query = database.WeeklyUserEntry.AsQueryable();
            if (chartId != null) query = query.Where(w => w.ChartId == chartId);

            return (await query.ToArrayAsync(cancellationToken)).Select(q => new WeeklyTournamentEntry(q.UserId,
                q.ChartId, q.Score, Enum.Parse<PhoenixPlate>(q.Plate), q.IsBroken,
                q.Photo == null ? null : new Uri(q.Photo, UriKind.Absolute), q.CompetitiveLevel));
        }

        public async Task SaveEntry(WeeklyTournamentEntry entry, CancellationToken cancellationToken)
        {
            var entity = await
                database.WeeklyUserEntry.FirstOrDefaultAsync(
                    e => e.UserId == entry.UserId && e.ChartId == entry.ChartId, cancellationToken);
            if (entity == null)
            {
                await database.WeeklyUserEntry.AddAsync(new WeeklyUserEntry
                {
                    ChartId = entry.ChartId,
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

        public async Task<IEnumerable<DateTimeOffset>> GetPastDates(CancellationToken cancellationToken)
        {
            return await database.UserWeeklyPlacing.Select(u => u.ObtainedDate).Distinct()
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<WeeklyTournamentEntry>> GetPastEntries(DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            return (await database.UserWeeklyPlacing
                .Where(e => e.ObtainedDate == date).ToArrayAsync(cancellationToken)).Select(u =>
                new WeeklyTournamentEntry(u.UserId, u.ChartId, u.Score, Enum.Parse<PhoenixPlate>(u.Plate), u.IsBroken,
                    null, u.CompetitiveLevel));
        }
    }
}
