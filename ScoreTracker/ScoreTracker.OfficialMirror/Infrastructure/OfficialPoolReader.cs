using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
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
    private readonly IOfficialSnapshotRepository _snapshots;

    public OfficialPoolReader(IOfficialSnapshotRepository snapshots,
        IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _snapshots = snapshots;
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
        // OfficialOnly, and not by default: supplemented rows are this site's own additions to
        // piugame's boards, and counting them would let our data vote in our own census of what
        // the world plays (supplemented-leaderboards.md §7).
        var highs = await _snapshots.GetChartBoardHighs(mix, PlacementScope.OfficialOnly, cancellationToken);
        if (highs.Count == 0) return Array.Empty<OfficialPricedPool>();

        // Board players already linked to a site account are dropped: they count once, through
        // their own records, which carry real plates.
        var linked = await LinkedPlayerIds(mix, cancellationToken);
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var byPlayer = new Dictionary<int, List<OfficialPricedChart>>();
        foreach (var high in highs)
        {
            if (linked.Contains(high.PlayerId)) continue;
            if (!Enum.TryParse<ChartType>(high.ChartType, out var chartType)) continue;
            if (chartType is not (ChartType.Single or ChartType.Double)) continue;
            var score = PhoenixScore.From((int)high.Score);
            var value = scoring.GetScore(chartType, DifficultyLevel.From(high.Level), score,
                ScoringConfiguration.ExpectedPlateForScore(score));
            if (value <= 0) continue;
            (byPlayer.TryGetValue(high.PlayerId, out var list) ? list : byPlayer[high.PlayerId] = new())
                .Add(new OfficialPricedChart(high.ChartId, chartType, value));
        }

        return byPlayer.Select(kv => new OfficialPricedPool(kv.Key,
                kv.Value.OrderByDescending(c => c.Value).ThenBy(c => c.ChartId).ToArray()))
            .ToArray();
    }

    /// <summary>
    ///     The player dimension, not the placement table — a link is a property of the tag, so
    ///     this read carries no placement scope to get wrong.
    /// </summary>
    private async Task<IReadOnlySet<int>> LinkedPlayerIds(MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var ids = await database.Set<OfficialPlayerEntity>()
            .Where(p => p.MixId == mixId && p.UserId != null)
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
        return ids.ToHashSet();
    }
}
