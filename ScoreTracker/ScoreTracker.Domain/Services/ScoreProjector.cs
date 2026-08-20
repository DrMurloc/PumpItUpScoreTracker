using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Gathers the peers <see cref="PeerEstimator" /> estimates from: who counts as a peer,
///     what they scored, and — on Phoenix 1, the part that is easy to get subtly wrong — what
///     level each of them held at the moment they set that score.
///     <para>
///         Two definitions of "peer", one per mix, and nothing borrowed between them
///         (docs/design/pumbility-overhaul.md D21):
///     </para>
///     <list type="bullet">
///         <item>
///             <b>Phoenix 1</b> (§4.1): players within a competitive-level band of the viewer,
///             their scores in this mix, each discounted by how far they have grown since setting
///             it, read at the 65th percentile.
///         </item>
///         <item>
///             <b>Phoenix 2</b> (§4.8): PUMBILITY peers — players within ±3 rungs of the viewer
///             on the PUMBILITY level ladder who hold a full pool of the chart type, as the viewer
///             must — their Phoenix 2 scores at full voice, read at the median, and no opinion at
///             all under five of them. Every number here is one the Phoenix 2 boards can confirm.
///         </item>
///     </list>
///     <para>
///         The arithmetic lives in <see cref="PeerEstimator" />, which is pure. This is the
///         plumbing around it, and it sits here rather than inside a vertical because two
///         verticals need it and neither may reference the other.
///     </para>
/// </summary>
public sealed class ScoreProjector : IScoreProjector
{
    private readonly IPlayerHistoryRepository _history;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _stats;

    public ScoreProjector(IScoreReader scores, IPlayerStatsReader stats, IPlayerHistoryRepository history)
    {
        _scores = scores;
        _stats = stats;
        _history = history;
    }

    public Task<ScoreProjection> Project(ScoreProjectionRequest request, CancellationToken cancellationToken)
    {
        if (request.Targets.Count == 0) return Task.FromResult(ScoreProjection.None());

        return request.Mix == MixEnum.Phoenix2
            ? ProjectFromPumbilityPeers(request, cancellationToken)
            : ProjectFromCompetitiveBand(request, cancellationToken);
    }

    public async Task<double> CompetitiveLevel(MixEnum mix, ChartType chartType, Guid userId,
        CancellationToken cancellationToken)
    {
        // The player is measured in the mix they are looking at, and only there. The Phoenix 2
        // launch fallback that read the other mix's level for an account with no scores here
        // went with the cross-mix reads (D21): the mix's own number, or the no-data floor.
        return CompetitiveLevelFor(await _stats.GetStats(mix, userId, cancellationToken), chartType);
    }

    // ------------------------------------------------------------------ Phoenix 1

    private async Task<ScoreProjection> ProjectFromCompetitiveBand(ScoreProjectionRequest request,
        CancellationToken cancellationToken)
    {
        var (mix, chartType, userId, targets, window, catalog, _, _, _) = request;

        var myLevel = await CompetitiveLevel(mix, chartType, userId, cancellationToken);
        // Competitive level 1 is the no-data floor: there is no band to draw peers from.
        if (myLevel <= 1) return ScoreProjection.None(myLevel);

        // The band IS the peer group here — the viewer out, nobody gated on a pool (D43): a
        // competitive level is real at any pool size, unlike a PUMBILITY rung.
        var peers = (await _stats.GetPlayersByCompetitiveRange(mix, chartType, myLevel, window, cancellationToken))
            .ToHashSet();
        peers.Remove(userId);
        var group = PeerGroup.Competitive(myLevel, window, peers.Count);
        if (peers.Count == 0) return ScoreProjection.None(myLevel, group);

        // The target set spans a level band, so it is asked for as one: a range scan the index
        // can serve, rather than several hundred chart GUIDs in an IN list. The exact set is
        // applied in memory below, so the result is identical either way. When the caller brought
        // the catalog, the read reaches down to the pool floor instead, so the same read also
        // says what the band's pools are made of (D43) — the floor is the formula's own: nothing
        // below level 10 prices above zero on either mix.
        var wanted = targets.Select(t => t.ChartId).ToHashSet();
        var records = catalog == null
            ? (await _scores.GetPlayerScoresInLevelRange(mix, peers, chartType,
                targets.Min(t => t.Level), targets.Max(t => t.Level), cancellationToken)).ToArray()
            : (await _scores.GetPlayerScoresInLevelRange(mix, peers, chartType,
                PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, cancellationToken)).ToArray();
        var pools = catalog == null
            ? null
            : PumbilityPeerPools.Build(records, peers, catalog, ScoringConfiguration.PumbilityScoring(mix, false));
        var peerScores = records.Where(s => wanted.Contains(s.ChartId)).ToArray();

        // Their level NOW, and their level history, so a score can be dated against the player
        // they were when they set it. History is read for the peers who ACTUALLY turned up on
        // the targets, not for the whole band — a band is several hundred players and each carries
        // a full timeline, so the ones who never played one of these charts were pure freight.
        var voices = peerScores.Select(s => s.UserId).Distinct().ToArray();
        if (voices.Length == 0)
            return new ScoreProjection(new Dictionary<Guid, PhoenixScore>(), 0, myLevel, 0, group,
                new Dictionary<Guid, PeerSpread>(), pools);

        var levelNow = (await _stats.GetStats(mix, voices, cancellationToken))
            .ToDictionary(s => s.UserId, s => CompetitiveLevelFor(s, chartType));
        var history = (await _history.GetHistory(mix, voices, cancellationToken))
            .GroupBy(h => h.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.Date).ToArray());

        var projected = new Dictionary<Guid, PhoenixScore>();
        var spreads = new Dictionary<Guid, PeerSpread>();
        // Counted from the scores that actually reached an estimate rather than from the sweep:
        // a peer with no stats row is dropped below, so the sweep's own distinct count would name
        // players whose evidence never got used.
        var contributors = new HashSet<Guid>();
        var freshnessSum = 0.0;
        var freshnessCount = 0;
        foreach (var chart in peerScores.GroupBy(s => s.ChartId))
        {
            var contributing = chart.Where(s => levelNow.ContainsKey(s.UserId)).ToArray();
            var scored = contributing
                .Select(s => new PeerScore((int)s.Score, levelNow[s.UserId],
                    LevelWhenSet(history, s.UserId, s.RecordedAt, chartType, levelNow[s.UserId])))
                .ToArray();

            var estimate = PeerEstimator.Estimate(scored);
            if (estimate == null) continue;

            // Per score rather than per player: the question a caller asks of this is how heavily
            // the EVIDENCE is discounted, and a peer who lent five scores lent five pieces of it.
            for (var i = 0; i < scored.Length; i++)
            {
                contributors.Add(contributing[i].UserId);
                freshnessSum += PeerEstimator.GrowthWeight(scored[i].Growth);
            }

            freshnessCount += scored.Length;
            projected[chart.Key] = estimate.Value;
            spreads[chart.Key] = SpreadOf(scored, PeerEstimator.GrowthDecayLevels);
        }

        return new ScoreProjection(projected, contributors.Count, myLevel,
            freshnessCount == 0 ? 0 : freshnessSum / freshnessCount, group, spreads, pools);
    }

    /// <summary>
    ///     The competitive level this player held when the score landed, from their own history.
    ///     A score older than every history row takes the EARLIEST row rather than the player's
    ///     level today: PlayerHistory begins 2024-06-04, and the level they held at the start of
    ///     the record is the closest thing to the level they held before it. Their current level
    ///     is the fallback only when there is no history at all, which would otherwise credit the
    ///     score with every point of growth since — the reverse of what an old score deserves.
    /// </summary>
    private static double LevelWhenSet(IReadOnlyDictionary<Guid, PlayerRatingRecord[]> history, Guid userId,
        DateTimeOffset? recordedAt, ChartType chartType, double fallback)
    {
        if (recordedAt == null || !history.TryGetValue(userId, out var rows) || rows.Length == 0)
            return fallback;

        PlayerRatingRecord? preceding = null;
        foreach (var row in rows)
        {
            if (row.Date > recordedAt.Value) break;
            preceding = row;
        }

        var chosen = preceding ?? rows[0];
        var level = chartType == ChartType.Single ? chosen.SinglesLevel : chosen.DoublesLevel;
        return level > 1 ? level : fallback;
    }

    // ------------------------------------------------------------------ Phoenix 2

    private async Task<ScoreProjection> ProjectFromPumbilityPeers(ScoreProjectionRequest request,
        CancellationToken cancellationToken)
    {
        var (mix, chartType, userId, targets, _, catalog, _, projectedTotal, totalIsEstimate) = request;

        var mine = await _stats.GetStats(mix, userId, cancellationToken);
        var myLevel = CompetitiveLevelFor(mine, chartType);

        // The viewer's own pool of the type first, and alone. On Phoenix 2 every non-broken pass
        // at the pool floor or above prices above zero, so a player's records of the type at
        // those levels ARE their pool. A short one is the common case at a mix launch and costs
        // one player's records to find out.
        var ownPool = PoolCount(await _scores.GetPlayerScoresInLevelRange(mix, new[] { userId }, chartType,
            PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, cancellationToken));

        // The viewer's rung, from the total pool — the merged top fifty across both types, which
        // is the number the game's own badge is drawn from. One rung serves both chart types. A
        // caller that can say where this player will finish supplies it, and the run is placed and
        // gated by that instead (D48): a twenty-chart pool's own total seats a strong player at the
        // bottom of the ladder, among peers who would tell them nothing. Without one the band is
        // not swept for a viewer it cannot yet serve (D28) — the page says how far they are rather
        // than estimating.
        var gate = projectedTotal is null ? PeerGroup.PumbilityPoolSize : PeerGroup.PumbilityProjectionGate;
        var rung = Phoenix2PumbilityLevel.From(projectedTotal ?? mine.SkillRating);
        if (ownPool < gate)
            return ScoreProjection.None(myLevel,
                PeerGroup.Pumbility(rung.Index, 0, ownPool, gate, totalIsEstimate));

        var (lowestIndex, highestIndex) = PeerGroup.PumbilityBand(rung.Index);
        var lowest = Phoenix2PumbilityLevel.FromIndex(lowestIndex)!.Value;
        var highest = Phoenix2PumbilityLevel.FromIndex(highestIndex)!.Value;

        // Half-open on the top: a rung's NextThreshold is where the rung above starts. The
        // capstone has nothing above it, so a band reaching it is open-ended. The viewer is never
        // one of their own peers.
        var candidates = (await _stats.GetPlayersByPumbilityRange(mix, lowest.Threshold,
                highest.NextThreshold ?? double.MaxValue, cancellationToken))
            .ToHashSet();
        candidates.Remove(userId);

        // One read answers two questions: what everyone in the band scored on the charts asked
        // about, and which of them hold a full pool of the type. A PEER's gate stays fifty
        // whatever the viewer's was: their pool is the evidence, and half a pool is half a vote.
        var records = (await _scores.GetPlayerScoresInLevelRange(mix, candidates, chartType,
                PeerGroup.PumbilityPoolFloor, DifficultyLevel.Max, cancellationToken))
            .ToArray();
        var peers = records
            .GroupBy(s => s.UserId)
            .Where(g => PoolCount(g) >= PeerGroup.PumbilityPoolSize)
            .Select(g => g.Key)
            .ToHashSet();
        var group = PeerGroup.Pumbility(rung.Index, peers.Count, ownPool, gate, totalIsEstimate);
        if (peers.Count == 0) return ScoreProjection.None(myLevel, group);

        // The peers' pools ride the same read when the caller brought the catalog to price it with
        // (§3.10): every chart they hold and everything they scored, the viewer already out.
        var pools = catalog == null
            ? null
            : PumbilityPeerPools.Build(records, peers, catalog, ScoringConfiguration.PumbilityScoring(mix, false));

        var wanted = targets.Select(t => t.ChartId).ToHashSet();
        var heard = records
            .Where(s => peers.Contains(s.UserId) && wanted.Contains(s.ChartId))
            .GroupBy(s => s.ChartId)
            .ToArray();

        var (projected, spreads, contributors) = Estimate(heard, PeerEstimator.Phoenix2MinimumPeers);

        // The floor asks for five peers ON A CHART, not five in the band, so at a band size of
        // five it is asking for unanimity and at three it can never be met at all. That is a
        // cliff rather than a filter: the thinnest bands — the bottom of the ladder and the top —
        // go from a full board to nothing over one player. Where it leaves the run with literally
        // nothing, the same records answer again with no floor: one peer's score is thin evidence
        // and the page says so, but it is evidence, and an empty board is not (D47).
        if (projected.Count == 0 && request.RelaxFloorWhenEmpty)
            (projected, spreads, contributors) = Estimate(heard, 1);

        return new ScoreProjection(projected, contributors.Count, myLevel, 1.0, group, spreads, pools);
    }

    /// <summary>
    ///     The Phoenix 2 estimate over records already grouped by chart, at the given floor. A
    ///     chart too few peers scored is absent rather than zero, which is what
    ///     <paramref name="minimumPeers" /> decides.
    /// </summary>
    private static (Dictionary<Guid, PhoenixScore> Projected, Dictionary<Guid, PeerSpread> Spreads,
        HashSet<Guid> Contributors) Estimate(IEnumerable<IGrouping<Guid, UserPhoenixScore>> heard, int minimumPeers)
    {
        var projected = new Dictionary<Guid, PhoenixScore>();
        var spreads = new Dictionary<Guid, PeerSpread>();
        var contributors = new HashSet<Guid>();
        foreach (var chart in heard)
        {
            var voices = chart.ToArray();
            // Full voice for every score: nothing here is dated against a level (D25).
            var scored = voices.Select(s => new PeerScore((int)s.Score, 0, 0)).ToArray();
            var estimate = PeerEstimator.Estimate(scored, 0, PeerEstimator.Phoenix2Quantile, minimumPeers);
            if (estimate == null) continue;

            foreach (var voice in voices) contributors.Add(voice.UserId);
            projected[chart.Key] = estimate.Value;
            spreads[chart.Key] = SpreadOf(scored, 0);
        }

        return (projected, spreads, contributors);
    }

    /// <summary>
    ///     The peers' first and third quartiles on one chart, read exactly as the estimate is —
    ///     same scores, same growth weights, same quantile arithmetic — so a page's "Peers IQR"
    ///     brackets the very median it prints. Called only after the estimate exists, so the
    ///     quartiles always do too.
    /// </summary>
    private static PeerSpread SpreadOf(IReadOnlyCollection<PeerScore> scored, double growthDecayLevels)
    {
        var lower = PeerEstimator.Estimate(scored, growthDecayLevels, PeerEstimator.LowerQuartile)!.Value;
        var upper = PeerEstimator.Estimate(scored, growthDecayLevels, PeerEstimator.UpperQuartile)!.Value;
        return new PeerSpread(lower, upper, scored.Count);
    }

    /// <summary>Distinct charts among a player's records of the type — their pool of it.</summary>
    private static int PoolCount(IEnumerable<UserPhoenixScore> records)
    {
        return records.Select(s => s.ChartId).Distinct().Count();
    }

    private static double CompetitiveLevelFor(PlayerStatsRecord stats, ChartType chartType)
    {
        return chartType switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        };
    }
}
