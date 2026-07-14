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
                SignalsJson = JsonSerializer.Serialize(new SignalsDto(edge.StyleScore, edge.BehaviorScore,
                    edge.PlayersScore, edge.IntensityScore, edge.MetaScore)),
                SharedScorers = edge.SharedScorers,
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
                var signals = JsonSerializer.Deserialize<SignalsDto>(e.SignalsJson) ??
                              new SignalsDto(null, null, null, null, null);
                return new ChartSimilarityEdge(e.SimilarChartId, e.Score, signals.Style, signals.Behavior,
                    signals.Players, signals.Intensity, signals.Meta, e.SharedScorers);
            })
            .ToArray();
    }

    // The persisted breakdown shape — additive-only: the why-chips tolerate missing keys,
    // so new signals can join without rewriting banked edges.
    private sealed record SignalsDto(double? Style, double? Behavior, double? Players, double? Intensity,
        double? Meta);
}
