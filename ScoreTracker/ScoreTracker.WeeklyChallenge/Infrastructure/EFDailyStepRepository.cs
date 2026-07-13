using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Domain;
using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.WeeklyChallenge.Infrastructure
{
    // One EF adapter serving both the vertical-internal write/read port and the published
    // reader slice — same shape as EFWeeklyTourneyRepository doubling as IWeeklyPlacingReader.
    internal sealed class EFDailyStepRepository
        (IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
        : IDailyStepRepository, IDailyStepReader
    {
        private static string CurrentChartKey(MixEnum mix)
        {
            return $@"{nameof(EFDailyStepRepository)}__CurrentChart__{mix}";
        }

        // 0–1 boards per mix; cached like the weekly board so N widget reads share one query.
        private async Task<DailyStepBoard[]> GetBoards(MixEnum mix, CancellationToken cancellationToken)
        {
            var boards = await cache.GetOrCreateAsync(CurrentChartKey(mix), async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                await using var database = await factory.CreateDbContextAsync(cancellationToken);
                var mixId = MixIds.For(mix);
                return await database.Set<DailyStepChartEntity>().Where(e => e.MixId == mixId)
                    .Select(e => new DailyStepBoard(e.ChartId, e.ForDate, e.IsLimbo, e.ExpirationDate))
                    .ToArrayAsync(cancellationToken);
            });
            return boards ?? Array.Empty<DailyStepBoard>();
        }

        public async Task<DailyStepBoard?> GetCurrentChart(MixEnum mix, CancellationToken cancellationToken)
        {
            return (await GetBoards(mix, cancellationToken)).FirstOrDefault();
        }

        async Task<IEnumerable<Guid>> IDailyStepReader.GetCurrentChartIds(MixEnum mix,
            CancellationToken cancellationToken)
        {
            return (await GetBoards(mix, cancellationToken)).Select(b => b.ChartId).ToArray();
        }

        public async Task RegisterDailyChart(MixEnum mix, DailyStepBoard board, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            await database.Set<DailyStepChartEntity>().AddAsync(new DailyStepChartEntity
            {
                ChartId = board.ChartId,
                MixId = MixIds.For(mix),
                ForDate = board.ForDate,
                IsLimbo = board.IsLimbo,
                ExpirationDate = board.ExpirationDate
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            cache.Remove(CurrentChartKey(mix));
        }

        public async Task ClearBoard(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entries = await database.Set<DailyStepEntryEntity>().Where(e => e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            var charts = await database.Set<DailyStepChartEntity>().Where(e => e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            database.Set<DailyStepEntryEntity>().RemoveRange(entries);
            database.Set<DailyStepChartEntity>().RemoveRange(charts);
            await database.SaveChangesAsync(cancellationToken);
            cache.Remove(CurrentChartKey(mix));
        }

        public async Task<IEnumerable<DailyStepEntry>> GetEntries(MixEnum mix, Guid? chartId,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var query = database.Set<DailyStepEntryEntity>().Where(e => e.MixId == mixId);
            if (chartId != null) query = query.Where(e => e.ChartId == chartId);

            return (await query.ToArrayAsync(cancellationToken)).Select(e => new DailyStepEntry(e.UserId,
                e.ChartId, e.Score, Enum.Parse<PhoenixPlate>(e.Plate), e.IsBroken, e.CompetitiveLevel, e.Source));
        }

        public async Task SaveEntry(MixEnum mix, DailyStepEntry entry, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<DailyStepEntryEntity>().FirstOrDefaultAsync(
                e => e.UserId == entry.UserId && e.ChartId == entry.ChartId && e.MixId == mixId, cancellationToken);
            if (entity == null)
            {
                await database.Set<DailyStepEntryEntity>().AddAsync(new DailyStepEntryEntity
                {
                    ChartId = entry.ChartId,
                    MixId = mixId,
                    IsBroken = entry.IsBroken,
                    Plate = entry.Plate.ToString(),
                    Score = entry.Score,
                    UserId = entry.UserId,
                    CompetitiveLevel = entry.CompetitiveLevel,
                    Source = entry.Source
                }, cancellationToken);
            }
            else
            {
                entity.Plate = entry.Plate.ToString();
                entity.Score = entry.Score;
                entity.CompetitiveLevel = entry.CompetitiveLevel;
                entity.IsBroken = entry.IsBroken;
                entity.Source = entry.Source;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteHistories(MixEnum mix, IEnumerable<DailyStepPlacing> placings,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            await database.Set<UserDailyStepPlacingEntity>().AddRangeAsync(placings.Select(p =>
                new UserDailyStepPlacingEntity
                {
                    UserId = p.UserId,
                    ChartId = p.ChartId,
                    MixId = mixId,
                    ForDate = p.ForDate,
                    IsLimbo = p.IsLimbo,
                    Place = p.Place,
                    Score = p.Score,
                    Plate = p.Plate.ToString(),
                    IsBroken = p.IsBroken,
                    CompetitiveLevel = p.CompetitiveLevel
                }), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
