using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFChartSimilarityRepository : IChartSimilarityRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartSimilarityRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task ReplaceEdges(MixEnum mix, Guid chartId, IReadOnlyList<ChartSimilarityEdge> edges,
        DateTimeOffset computedAt, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<ChartSimilarityEntity>()
            .Where(e => e.MixId == mixId && e.ChartId == chartId)
            .ToArrayAsync(cancellationToken);
        database.Set<ChartSimilarityEntity>().RemoveRange(existing);
        await database.Set<ChartSimilarityEntity>().AddRangeAsync(edges.Select(edge =>
            new ChartSimilarityEntity
            {
                MixId = mixId,
                ChartId = chartId,
                SimilarChartId = edge.SimilarChartId,
                Score = edge.Score,
                SignalsJson = JsonSerializer.Serialize(new SignalsDto(edge.SkillScore, edge.IntensityScore)),
                ComputedAt = computedAt
            }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChartSimilarityEdge>> GetEdges(MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var entities = await database.Set<ChartSimilarityEntity>()
            .Where(e => e.MixId == mixId && e.ChartId == chartId)
            .ToArrayAsync(cancellationToken);
        return entities
            .OrderByDescending(e => e.Score)
            .Select(e =>
            {
                var signals = JsonSerializer.Deserialize<SignalsDto>(e.SignalsJson);
                return new ChartSimilarityEdge(e.SimilarChartId, e.Score, signals?.Skill ?? 0,
                    signals?.Intensity ?? 0);
            })
            .ToArray();
    }

    // The persisted breakdown shape. Nullable on the way in because an edge banked under
    // an older shape deserializes as missing rather than throwing; the nightly rebuild
    // rewrites every edge wholesale, so a stale row is transient by construction.
    private sealed record SignalsDto(double? Skill, double? Intensity);
}
