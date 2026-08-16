using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFPumbilityPoolCompositionRepository : IPumbilityPoolCompositionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPumbilityPoolCompositionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(PumbilityPoolCompositionRecord composition, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(composition.Mix);
        var existing = await database.Set<PumbilityPoolCompositionEntity>()
            .Where(e => e.MixId == mixId)
            .ToArrayAsync(cancellationToken);
        database.Set<PumbilityPoolCompositionEntity>().RemoveRange(existing);
        foreach (var band in composition.Bands)
            await database.Set<PumbilityPoolCompositionEntity>().AddAsync(new PumbilityPoolCompositionEntity
            {
                MixId = mixId,
                BandKey = band.Key,
                Title = band.Title,
                Floor = band.Floor,
                Ceiling = band.Ceiling,
                Players = band.Players,
                ChartsPooled = band.ChartsPooled,
                LevelSum = band.LevelSum,
                LevelPart = band.LevelPart,
                ScorePart = band.ScorePart,
                PlatePart = band.PlatePart,
                // Grade names rather than enum ordinals: the ladder has been re-ordered before, and
                // a name still means the same grade after it has.
                GradeCountsJson = JsonSerializer.Serialize(
                    band.GradeCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
                PoolsCounted = composition.PoolsCounted,
                ComputedAt = composition.ComputedAt
            }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<PumbilityPoolCompositionRecord?> Get(MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var rows = await database.Set<PumbilityPoolCompositionEntity>()
            .Where(e => e.MixId == mixId)
            .OrderBy(e => e.Floor)
            .ToArrayAsync(cancellationToken);
        if (rows.Length == 0) return null;

        return new PumbilityPoolCompositionRecord(mix, rows[0].ComputedAt, rows[0].PoolsCounted,
            rows.Select(e => new PumbilityPoolBandRecord(e.BandKey, e.Title, e.Floor, e.Ceiling, e.Players,
                e.ChartsPooled, e.LevelSum, e.LevelPart, e.ScorePart, e.PlatePart, ReadGrades(e.GradeCountsJson)))
                .ToArray());
    }

    private static IReadOnlyDictionary<PhoenixLetterGrade, int> ReadGrades(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        var grades = new Dictionary<PhoenixLetterGrade, int>();
        foreach (var (name, count) in raw)
            if (Enum.TryParse<PhoenixLetterGrade>(name, out var grade))
                grades[grade] = count;
        return grades;
    }
}
