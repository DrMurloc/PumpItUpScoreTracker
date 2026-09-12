using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     Board players' PUMBILITY pools, rebuilt from the mirrored per-chart boards
///     (docs/design/hardmode-leaderboard.md §2). The mirror is the only thing that can price a
///     board row: a row carries a score and no plate, so the plate has to be inferred, and that
///     inference is this vertical's business.
///     <para>
///         Two faces on one read. The Domain port hands the census chart ids in slot order,
///         which is all a slot weight needs; the internal source hands this vertical the values
///         too, because a board total built from ids alone would be the same number for every
///         player.
///     </para>
/// </summary>
internal sealed class OfficialPoolReader : IOfficialPoolReader, IOfficialPoolSource
{
    /// <summary>A pool is fifty. A player whose fifty the mirror cannot see in full does not vote.</summary>
    private const int PoolSize = 50;

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public OfficialPoolReader(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<OfficialPoolSlots>> GetFullPools(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var priced = await GetPricedPools(mix, cancellationToken);
        var pools = new List<OfficialPoolSlots>();
        foreach (var player in priced)
        {
            Add(player.OfficialPlayerId, null, player.Charts);
            Add(player.OfficialPlayerId, ChartType.Single,
                player.Charts.Where(c => c.ChartType == ChartType.Single));
            Add(player.OfficialPlayerId, ChartType.Double,
                player.Charts.Where(c => c.ChartType == ChartType.Double));
        }

        return pools;

        void Add(int playerId, ChartType? pool, IEnumerable<OfficialPricedChart> charts)
        {
            var fifty = charts.Take(PoolSize).ToArray();
            if (fifty.Length < PoolSize) return;
            pools.Add(new OfficialPoolSlots(playerId, pool, fifty.Select(f => f.ChartId).ToArray()));
        }
    }

    public async Task<IReadOnlyList<OfficialPricedPool>> GetPricedPools(MixEnum mix,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);

        // The best score the mirror holds per (player, chart) across every snapshot, because a
        // board is a top-N and a player drops off it as others pass them — the highest row we
        // ever saw is the closest thing to their record. Players already linked to a site
        // account are excluded: they count once, through their own records.
        var rows = await (
                from placement in database.Set<OfficialLeaderboardPlacementEntity>()
                join board in database.Set<OfficialLeaderboardEntity>()
                    on placement.LeaderboardId equals board.Id
                join player in database.Set<OfficialPlayerEntity>() on placement.PlayerId equals player.Id
                join chart in database.Set<ChartEntity>() on board.ChartId equals chart.Id
                join chartMix in database.Set<ChartMixEntity>() on chart.Id equals chartMix.ChartId
                where board.MixId == mixId && board.LeaderboardType == "Chart" && board.ChartId != null
                      && player.UserId == null && chartMix.MixId == mixId
                      && (chart.Type == "Single" || chart.Type == "Double")
                group new { placement.Score, chart.Type, chartMix.Level } by
                    new { placement.PlayerId, ChartId = chart.Id }
                into scores
                select new
                {
                    scores.Key.PlayerId,
                    scores.Key.ChartId,
                    Score = scores.Max(s => s.Score),
                    Type = scores.Min(s => s.Type),
                    Level = scores.Min(s => s.Level)
                })
            .ToArrayAsync(cancellationToken);

        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var byPlayer = new Dictionary<int, List<OfficialPricedChart>>();
        foreach (var row in rows)
        {
            if (!Enum.TryParse<ChartType>(row.Type, out var chartType)) continue;
            var score = PhoenixScore.From((int)row.Score);
            var value = scoring.GetScore(chartType, DifficultyLevel.From(row.Level), score,
                ScoringConfiguration.ExpectedPlateForScore(score));
            if (value <= 0) continue;
            (byPlayer.TryGetValue(row.PlayerId, out var list) ? list : byPlayer[row.PlayerId] = new())
                .Add(new OfficialPricedChart(row.ChartId, chartType, value));
        }

        return byPlayer.Select(kv => new OfficialPricedPool(kv.Key,
                kv.Value.OrderByDescending(c => c.Value).ThenBy(c => c.ChartId).ToArray()))
            .ToArray();
    }
}
