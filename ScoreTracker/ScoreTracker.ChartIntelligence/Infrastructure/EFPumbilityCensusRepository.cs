using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFPumbilityCensusRepository : IPumbilityCensusRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPumbilityCensusRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveFolder(MixEnum mix, ChartType chartType, DifficultyLevel level,
        IReadOnlyDictionary<string, PumbilityCensusFolder> byCohort,
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

    public async Task<PumbilityCensusFolder> GetFolder(MixEnum mix, ChartType chartType,
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
        return new PumbilityCensusFolder(
            rows.Select(e => new PumbilityCensusRecord(e.ChartId, e.Appearances,
                Enum.Parse<TierListCategory>(e.Category), e.Order)).ToArray(),
            rows.Length == 0 ? 0 : rows[0].CohortSize);
    }

    public async Task<IEnumerable<(ChartType ChartType, int Level)>> GetFoldersWithData(MixEnum mix,
        string cohortKey, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        // A folder every one of whose rows reads zero is a folder this cohort cannot speak
        // for — it is written, so the job does not have to remember which folders it skipped,
        // but it must not be offered.
        return (await database.Set<PumbilityCensusEntryEntity>()
                .Where(e => e.MixId == mixId && e.CohortKey == cohortKey && e.Appearances > 0)
                .Select(e => new { e.ChartType, e.Level })
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .Select(e => (Enum.Parse<ChartType>(e.ChartType), e.Level));
    }
}
