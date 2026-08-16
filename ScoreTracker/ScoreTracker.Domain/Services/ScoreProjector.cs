using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Gathers the peers <see cref="PeerEstimator" /> estimates from: who counts as a peer,
///     what they scored, and — the part that is easy to get subtly wrong — what level each of
///     them held at the moment they set that score.
///     <para>
///         The arithmetic lives in <see cref="PeerEstimator" />, which is pure. This is the
///         plumbing around it, and it sits here rather than inside a vertical because two
///         verticals need it and neither may reference the other.
///     </para>
/// </summary>
public sealed class ScoreProjector : IScoreProjector
{
    /// <summary>
    ///     Levels of slack when reading the reference mix, which rerated these charts — a chart
    ///     sitting at 21 here may sit at 22 or 20 there.
    /// </summary>
    private const int ReferenceLevelSlack = 2;

    private readonly IPlayerHistoryRepository _history;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _stats;

    public ScoreProjector(IScoreReader scores, IPlayerStatsReader stats, IPlayerHistoryRepository history)
    {
        _scores = scores;
        _stats = stats;
        _history = history;
    }

    public async Task<ScoreProjection> Project(ScoreProjectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Targets.Count == 0) return ScoreProjection.None();

        var (mix, chartType, userId, targets, window) = request;
        var reference = ReferenceMixFor(mix);

        var myLevel = await CompetitiveLevel(mix, chartType, userId, cancellationToken);
        // Competitive level 1 is the no-data floor: there is no band to draw peers from.
        if (myLevel <= 1) return ScoreProjection.None(myLevel);

        var cohort = (await _stats.GetPlayersByCompetitiveRange(mix, chartType, myLevel, window,
            cancellationToken)).ToHashSet();
        if (reference != mix)
            cohort.UnionWith(await _stats.GetPlayersByCompetitiveRange(reference, chartType, myLevel, window,
                cancellationToken));
        cohort.Remove(userId);
        if (cohort.Count == 0) return ScoreProjection.None(myLevel);

        var peerScores = await BestAcrossMixes(mix, reference, cohort, targets, chartType, cancellationToken);

        // Their level NOW, and their level history, so a score can be dated against the player
        // they were when they set it. Both come from the reference mix, which is where the level
        // series actually runs; a peer the reference mix has never seen falls back to this one.
        //
        // History is read for the peers who ACTUALLY turned up in the sweep, not for the whole
        // cohort — a cohort is several hundred players and each carries a full timeline, so the
        // ones who never played one of these charts were pure freight.
        var voices = peerScores.Select(s => s.UserId).Distinct().ToArray();
        if (voices.Length == 0) return ScoreProjection.None(myLevel);

        var levelNow = (await _stats.GetStats(reference, voices, cancellationToken))
            .ToDictionary(s => s.UserId, s => CompetitiveLevelFor(s, chartType));
        if (reference != mix)
            foreach (var stats in await _stats.GetStats(mix, voices, cancellationToken))
                if (!levelNow.ContainsKey(stats.UserId))
                    levelNow[stats.UserId] = CompetitiveLevelFor(stats, chartType);

        var history = (await _history.GetHistory(reference, voices, cancellationToken))
            .GroupBy(h => h.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.Date).ToArray());

        return Estimate(peerScores, levelNow, history, chartType, myLevel);
    }

    /// <summary>
    ///     One estimate per chart, plus the tally of what the estimates rested on. Split out of
    ///     <see cref="Project" />, which is otherwise a page of gathering followed by a page of
    ///     arithmetic and reads as neither.
    /// </summary>
    private static ScoreProjection Estimate(IReadOnlyCollection<UserPhoenixScore> peerScores,
        IReadOnlyDictionary<Guid, double> levelNow,
        IReadOnlyDictionary<Guid, PlayerRatingRecord[]> history, ChartType chartType, double myLevel)
    {
        var projected = new Dictionary<Guid, PhoenixScore>();
        // Counted from the scores that actually reached an estimate rather than from the sweep:
        // a peer the reference mix has no stats row for is dropped below, so the sweep's own
        // distinct count would name players whose evidence never got used.
        var contributors = new HashSet<Guid>();
        var freshnessSum = 0.0;
        var freshnessCount = 0;
        foreach (var group in peerScores.GroupBy(s => s.ChartId))
        {
            var contributing = group.Where(s => levelNow.ContainsKey(s.UserId)).ToArray();
            var peers = contributing
                .Select(s => new PeerScore((int)s.Score, levelNow[s.UserId],
                    LevelWhenSet(history, s.UserId, s.RecordedAt, chartType, levelNow[s.UserId])))
                .ToArray();

            var estimate = PeerEstimator.Estimate(peers);
            if (estimate == null) continue;

            // Per score rather than per player: the question a caller asks of this is how heavily
            // the EVIDENCE is discounted, and a peer who lent five scores lent five pieces of it.
            for (var i = 0; i < peers.Length; i++)
            {
                contributors.Add(contributing[i].UserId);
                freshnessSum += PeerEstimator.GrowthWeight(peers[i].Growth);
            }

            freshnessCount += peers.Length;
            projected[group.Key] = estimate.Value;
        }

        return new ScoreProjection(projected, contributors.Count, myLevel,
            freshnessCount == 0 ? 0 : freshnessSum / freshnessCount);
    }

    public async Task<double> CompetitiveLevel(MixEnum mix, ChartType chartType, Guid userId,
        CancellationToken cancellationToken)
    {
        // The player is measured in the mix they are looking at. The reference mix is a detail
        // of the PEER side only (see BestAcrossMixes) — except here, where a launch-mix account
        // with no scores yet has nothing to match peers on, and the other mix names a level
        // rather than the caller getting nothing at all.
        var reference = ReferenceMixFor(mix);
        var level = CompetitiveLevelFor(await _stats.GetStats(mix, userId, cancellationToken), chartType);
        if (level > 1 || reference == mix) return level;

        return CompetitiveLevelFor(await _stats.GetStats(reference, userId, cancellationToken), chartType);
    }

    /// <summary>
    ///     The mix a peer's evidence may also be read from. Phoenix 2 rerated Phoenix 1's charts
    ///     rather than restepping them, and the score scale did not move with them: across 2,241
    ///     player-chart pairs scored in both mixes the median difference is zero, with 976 higher
    ///     in Phoenix 2 against 994 lower. A changed scoring formula would show a consistent
    ///     offset — that spread is practice, so a peer's Phoenix 1 score is real evidence of what
    ///     they can do on the same steps today.
    ///     <para>
    ///         This matters most at a launch, which is exactly when the cohort is thinnest:
    ///         Phoenix 2 has scores from tens of players where Phoenix 1 has thousands.
    ///     </para>
    /// </summary>
    private static MixEnum ReferenceMixFor(MixEnum mix)
    {
        return mix == MixEnum.Phoenix2 ? MixEnum.Phoenix : mix;
    }

    /// <summary>
    ///     Each peer's best attempt per chart across the projected mix and its reference mix.
    ///     Only the peers pool — the player's own scores are never read from another mix, because
    ///     what they are being shown is what they have done HERE.
    /// </summary>
    private async Task<IReadOnlyCollection<UserPhoenixScore>> BestAcrossMixes(MixEnum mix, MixEnum reference,
        IReadOnlyCollection<Guid> cohort, IReadOnlyCollection<ProjectionTarget> targets, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var wanted = targets.Select(t => t.ChartId).ToHashSet();
        // The target set spans a level band, so it is asked for as one: a range scan the index
        // can serve, rather than several hundred chart GUIDs in an IN list. The exact set is
        // applied in memory below, so the result is identical either way.
        var min = targets.Min(t => t.Level);
        var max = targets.Max(t => t.Level);

        var scores = (await _scores.GetPlayerScoresInLevelRange(mix, cohort, chartType, min, max,
            cancellationToken)).ToList();

        if (reference != mix)
            // Widened, because the reference mix rerated these charts and a chart's level there
            // is not necessarily its level here. Over-fetching costs a few rows; the wanted-set
            // filter below makes it exact.
            scores.AddRange(await _scores.GetPlayerScoresInLevelRange(reference, cohort, chartType,
                Math.Max(1, min - ReferenceLevelSlack), Math.Min(DifficultyLevel.Max, max + ReferenceLevelSlack),
                cancellationToken));

        return scores
            .Where(s => wanted.Contains(s.ChartId))
            .GroupBy(s => (s.UserId, s.ChartId))
            .Select(g => g.OrderByDescending(s => (int)s.Score).First())
            .ToArray();
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
