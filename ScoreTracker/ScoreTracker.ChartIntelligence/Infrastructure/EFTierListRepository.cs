using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure
{
    internal sealed class EFTierListRepository : ITierListRepository
    {
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly IScoreReader _scores;
        private readonly ITitleRepository _titles;

        public EFTierListRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IMemoryCache cache,
            IScoreReader scores,
            ITitleRepository titles)
        {
            _cache = cache;
            _factory = factory;
            _scores = scores;
            _titles = titles;
        }

        private static string TierListKey(MixEnum mix, Name tierListName)
        {
            return $"{nameof(EFTierListRepository)}_TierList_{mix}_{tierListName}";
        }

        public async Task SaveEntry(MixEnum mix, SongTierListEntry entry, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var type = entry.TierListName.ToString();
            var mixId = MixIds.For(mix);
            var entity = await database.Set<TierListEntryEntity>()
                .Where(e => e.TierListName == type && e.ChartId == entry.ChartId && e.MixId == mixId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.Set<TierListEntryEntity>().AddAsync(new TierListEntryEntity
                {
                    Id = Guid.NewGuid(),
                    Category = entry.Category.ToString(),
                    ChartId = entry.ChartId,
                    TierListName = type,
                    MixId = mixId,
                    Order = entry.Order
                });
            }
            else
            {
                entity.Category = entry.Category.ToString();
                entity.Order = entry.Order;
            }

            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(TierListKey(mix, entry.TierListName));
        }

        public async Task<IEnumerable<Guid>> GetUsersOnLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken, bool requireActive = false)
        {
            // Title cohorts come from PlayerProgress's ITitleRepository and activity from
            // the Ledger's IScoreReader — reads through published contracts, not joins onto
            // other verticals' tables (UserHighestTitle went PlayerProgress-internal at C50).
            var onLevel = await _titles.GetUserIdsOnHighestLevel(mix, level, cancellationToken);
            if (!requireActive)
                return onLevel;

            var cutoff = DateTimeOffset.Now - TimeSpan.FromDays(120);
            var active = await _scores.GetActiveUserIds(mix, cutoff, cancellationToken);
            return onLevel.Where(active.Contains).ToArray();
        }


        public async Task<IEnumerable<SongTierListEntry>> GetAllEntries(MixEnum mix, Name tierListName,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(TierListKey(mix, tierListName), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                var nameString = tierListName.ToString();
                var mixId = MixIds.For(mix);
                return (await database.Set<TierListEntryEntity>()
                        .Where(e => e.MixId == mixId).ToArrayAsync(cancellationToken))
                    .Where(e => e.TierListName == nameString).Select(e =>
                        new SongTierListEntry(e.TierListName,
                            e.ChartId, Enum.Parse<TierListCategory>(e.Category), e.Order));
            });
        }

        public async Task SaveEntries(MixEnum mix, IEnumerable<SongTierListEntry> entries,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entryArray = entries.ToArray();
            var mixId = MixIds.For(mix);
            var tierLists = entryArray.Select(e => e.TierListName.ToString()).Distinct().ToArray();
            var chartIds = entryArray.Select(e => e.ChartId).Distinct().ToArray();
            var entities = (await database.Set<TierListEntryEntity>()
                    .Where(e => tierLists.Contains(e.TierListName) && chartIds.Contains(e.ChartId)
                                                                   && e.MixId == mixId)
                    .ToArrayAsync(cancellationToken))
                .GroupBy(e => e.TierListName)
                .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.ChartId));


            foreach (var entry in entryArray)
            {
                var entity = entities.TryGetValue(entry.TierListName, out var list)
                    ? list.TryGetValue(entry.ChartId, out var r) ? r : null
                    : null;
                if (entity == null)
                {
                    var type = entry.TierListName.ToString();
                    await database.Set<TierListEntryEntity>().AddAsync(new TierListEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Category = entry.Category.ToString(),
                        ChartId = entry.ChartId,
                        TierListName = type,
                        MixId = mixId,
                        Order = entry.Order
                    }, cancellationToken);
                }
                else
                {
                    entity.Category = entry.Category.ToString();
                    entity.Order = entry.Order;
                }
            }


            await database.SaveChangesAsync(cancellationToken);

            foreach (var name in tierLists) _cache.Remove(TierListKey(mix, name));
        }

        public async Task SavePumbilityTierLists(MixEnum mix, ChartType chartType, DifficultyLevel level,
            IReadOnlyDictionary<string, PumbilityTierListFolder> byCohort,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var typeName = chartType.ToString();
            var levelInt = (int)level;
            var existing = await database.Set<PumbilityCensusEntryEntity>()
                .Where(e => e.MixId == mixId && e.ChartType == typeName && e.Level == levelInt)
                .ToArrayAsync(cancellationToken);
            database.Set<PumbilityCensusEntryEntity>().RemoveRange(existing);
            foreach (var (cohortKey, folder) in byCohort)
            foreach (var entry in folder.Entries)
                await database.Set<PumbilityCensusEntryEntity>().AddAsync(new PumbilityCensusEntryEntity
                {
                    MixId = mixId,
                    ChartType = typeName,
                    Level = levelInt,
                    CohortKey = cohortKey,
                    ChartId = entry.ChartId,
                    Appearances = entry.Appearances,
                    CohortSize = folder.CohortSize,
                    Category = entry.Category.ToString(),
                    Order = entry.Order
                }, cancellationToken);

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<PumbilityTierListFolder> GetPumbilityTierList(MixEnum mix, ChartType chartType,
            DifficultyLevel level, string cohortKey, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var typeName = chartType.ToString();
            var levelInt = (int)level;
            var rows = await database.Set<PumbilityCensusEntryEntity>()
                .Where(e => e.MixId == mixId && e.ChartType == typeName && e.Level == levelInt
                            && e.CohortKey == cohortKey)
                .ToArrayAsync(cancellationToken);
            return new PumbilityTierListFolder(
                rows.Select(e => new PumbilityTierListRecord(e.ChartId, e.Appearances,
                    Enum.Parse<TierListCategory>(e.Category), e.Order)).ToArray(),
                rows.Length == 0 ? 0 : rows[0].CohortSize);
        }

        public async Task<IEnumerable<(ChartType ChartType, int Level)>> GetPumbilityTierListFolders(
            MixEnum mix, string cohortKey, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            // A folder every one of whose rows reads zero is a folder this cohort cannot speak
            // for — it is written, so the rebuild does not have to remember which folders it
            // skipped, but it must not be offered.
            return (await database.Set<PumbilityCensusEntryEntity>()
                    .Where(e => e.MixId == mixId && e.CohortKey == cohortKey && e.Appearances > 0)
                    .Select(e => new { e.ChartType, e.Level })
                    .Distinct()
                    .ToArrayAsync(cancellationToken))
                .Select(e => (Enum.Parse<ChartType>(e.ChartType), e.Level));
        }
    }
}
