using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     The settled similarity formula (docs/design/chart-similarity.md). Pure: the saga
///     feeds one (mix, chart-type) pool of features, this returns each chart's top
///     neighbors. Hard gates — level within ±2, never the same song (siblings are
///     navigation, not discovery); the pool itself carries the same-mix/same-type gates.
///     Five signals in [0,1], combined as a weighted mean renormalized over whichever
///     signals the pair has, times a level-affinity factor; an edge needs at least two
///     non-metadata signals (metadata alone never makes a neighbor), a 0.55 floor, and
///     only the best eight survive. Weights are product behavior — tuned by PR after the
///     calibration eyeball, never config.
/// </summary>
internal static class ChartSimilarityCalculator
{
    internal const double StyleWeight = 0.30;
    internal const double BehaviorWeight = 0.25;
    internal const double PlayersWeight = 0.25;
    internal const double IntensityWeight = 0.10;
    internal const double MetaWeight = 0.10;

    internal const int LevelWindow = 2;
    internal const double LevelAffinityStep = 0.15;
    internal const int MinimumSharedScorers = 30;
    internal const double ScoreFloor = 0.55;
    internal const int TopK = 8;

    public static IReadOnlyDictionary<Guid, IReadOnlyList<ChartSimilarityEdge>> BuildEdges(
        IReadOnlyList<ChartSimilarityFeatures> pool)
    {
        var intensityZ = ComputeIntensityZScores(pool);
        var candidates = pool.ToDictionary(c => c.ChartId, _ => new List<ChartSimilarityEdge>());

        for (var i = 0; i < pool.Count; i++)
        for (var j = i + 1; j < pool.Count; j++)
        {
            var a = pool[i];
            var b = pool[j];
            var levelDistance = Math.Abs(a.Level - b.Level);
            if (levelDistance > LevelWindow) continue;
            if (a.SongName.Equals(b.SongName)) continue;

            var style = StyleSimilarity(a, b);
            var behavior = BehaviorSimilarity(a, b);
            var (players, sharedScorers) = PlayerSimilarity(a, b);
            var intensity = IntensitySimilarity(intensityZ[a.ChartId], intensityZ[b.ChartId]);
            var meta = MetaSimilarity(a, b);

            var nonMetaAvailable = new[] { style, behavior, players, intensity }.Count(s => s != null);
            if (nonMetaAvailable < 2) continue;

            var weightedSum = 0.0;
            var weightTotal = 0.0;
            Fold(style, StyleWeight);
            Fold(behavior, BehaviorWeight);
            Fold(players, PlayersWeight);
            Fold(intensity, IntensityWeight);
            Fold(meta, MetaWeight);

            var score = weightedSum / weightTotal * (1 - LevelAffinityStep * levelDistance);
            if (score < ScoreFloor) continue;

            candidates[a.ChartId]
                .Add(new ChartSimilarityEdge(b.ChartId, score, style, behavior, players, intensity, meta,
                    sharedScorers));
            candidates[b.ChartId]
                .Add(new ChartSimilarityEdge(a.ChartId, score, style, behavior, players, intensity, meta,
                    sharedScorers));
            continue;

            void Fold(double? signal, double weight)
            {
                if (signal == null) return;
                weightedSum += signal.Value * weight;
                weightTotal += weight;
            }
        }

        return candidates.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<ChartSimilarityEdge>)kv.Value
                .OrderByDescending(e => e.Score)
                .Take(TopK)
                .ToArray());
    }

    /// <summary>Cosine over the union of mapped-skill weights; missing without banked skills.</summary>
    private static double? StyleSimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        if (a.SkillWeights.Count == 0 || b.SkillWeights.Count == 0) return null;
        var keys = a.SkillWeights.Keys.Union(b.SkillWeights.Keys);
        double dot = 0, magA = 0, magB = 0;
        foreach (var key in keys)
        {
            var va = a.SkillWeights.GetValueOrDefault(key);
            var vb = b.SkillWeights.GetValueOrDefault(key);
            dot += va * vb;
            magA += va * va;
            magB += vb * vb;
        }

        if (magA == 0 || magB == 0) return null;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    /// <summary>
    ///     Mean of the available difficulty-behavior components: pass-tier distance,
    ///     score-tier distance (both over the 7-step ladder), letter-percentile curve
    ///     distance on the shared grade axis, and continuous scoring-level distance.
    /// </summary>
    private static double? BehaviorSimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        var components = new List<double>();
        if (TierIndex(a.PassTier) is { } passA && TierIndex(b.PassTier) is { } passB)
            components.Add(1 - Math.Abs(passA - passB) / 6.0);
        if (TierIndex(a.ScoreTier) is { } scoreA && TierIndex(b.ScoreTier) is { } scoreB)
            components.Add(1 - Math.Abs(scoreA - scoreB) / 6.0);
        if (a.LetterPercentiles != null && b.LetterPercentiles != null)
        {
            var shared = a.LetterPercentiles.Keys.Intersect(b.LetterPercentiles.Keys).ToArray();
            if (shared.Length > 0)
                components.Add(1 - shared.Average(g =>
                    Math.Abs(a.LetterPercentiles[g] - b.LetterPercentiles[g])));
        }

        if (a.ScoringLevel is { } slA && b.ScoringLevel is { } slB)
            components.Add(1 - Math.Min(1, Math.Abs(slA - slB) / 2.0));

        return components.Count == 0 ? null : components.Average();
    }

    private static int? TierIndex(TierListCategory? category)
    {
        return category is null or TierListCategory.Unrecorded ? null : (int)category.Value;
    }

    /// <summary>
    ///     max(0, pearson(residuals)) · n/(n+20) over the shared scorers — the
    ///     collaborative signal. Missing below the confidence floor or when either
    ///     side's residuals have no variance (correlation undefined). Negative
    ///     correlation clamps to present-but-zero: actively dissimilar weighs against
    ///     the pair, it never argues for it.
    /// </summary>
    private static (double? Score, int SharedScorers) PlayerSimilarity(ChartSimilarityFeatures a,
        ChartSimilarityFeatures b)
    {
        var (small, large) = a.ResidualByUser.Count <= b.ResidualByUser.Count
            ? (a.ResidualByUser, b.ResidualByUser)
            : (b.ResidualByUser, a.ResidualByUser);
        var pairs = new List<(double A, double B)>(small.Count);
        foreach (var (userId, residual) in small)
            if (large.TryGetValue(userId, out var other))
                pairs.Add((residual, other));

        var n = pairs.Count;
        if (n < MinimumSharedScorers) return (null, n);

        var meanA = pairs.Average(p => p.A);
        var meanB = pairs.Average(p => p.B);
        double covariance = 0, varianceA = 0, varianceB = 0;
        foreach (var (ra, rb) in pairs)
        {
            covariance += (ra - meanA) * (rb - meanB);
            varianceA += (ra - meanA) * (ra - meanA);
            varianceB += (rb - meanB) * (rb - meanB);
        }

        if (varianceA == 0 || varianceB == 0) return (null, n);
        var pearson = covariance / Math.Sqrt(varianceA * varianceB);
        return (Math.Max(0, pearson) * n / (n + 20.0), n);
    }

    /// <summary>
    ///     Step-analysis scalars z-scored within each chart's own (type, level) cohort —
    ///     the pool is one type, so the cohort is the level group. A cohort with no
    ///     spread pins z to 0 (everyone is the cohort norm).
    /// </summary>
    private static IReadOnlyDictionary<Guid, double?[]> ComputeIntensityZScores(
        IReadOnlyList<ChartSimilarityFeatures> pool)
    {
        var result = pool.ToDictionary(c => c.ChartId, _ => new double?[4]);
        foreach (var levelGroup in pool.GroupBy(c => c.Level))
        {
            var charts = levelGroup.ToArray();
            for (var dimension = 0; dimension < 4; dimension++)
            {
                var values = charts.Select(c => Scalar(c, dimension)).Where(v => v != null)
                    .Select(v => v!.Value).ToArray();
                if (values.Length == 0) continue;
                var mean = values.Average();
                var std = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
                foreach (var chart in charts)
                    if (Scalar(chart, dimension) is { } value)
                        result[chart.ChartId][dimension] = std == 0 ? 0 : (value - mean) / std;
            }
        }

        return result;

        static double? Scalar(ChartSimilarityFeatures c, int dimension)
        {
            return dimension switch
            {
                0 => c.Nps,
                1 => c.SustainFraction,
                2 => c.TensionFraction,
                _ => c.NoteCount
            };
        }
    }

    private static double? IntensitySimilarity(double?[] zA, double?[] zB)
    {
        var deltas = new List<double>(4);
        for (var dimension = 0; dimension < zA.Length; dimension++)
            if (zA[dimension] is { } a && zB[dimension] is { } b)
                deltas.Add(Math.Abs(a - b));
        if (deltas.Count == 0) return null;
        return Math.Clamp(1 - deltas.Average() / 3.0, 0, 1);
    }

    /// <summary>
    ///     0.5·sameStepArtist + 0.2·sameSongType + 0.2·bpmProximity + 0.1·sameDebutMix.
    ///     Always present — but never sufficient (the two-non-meta-signals gate).
    /// </summary>
    private static double MetaSimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        var artist = a.StepArtist != null && b.StepArtist != null && a.StepArtist.Value.Equals(b.StepArtist.Value)
            ? 0.5
            : 0.0;
        var songType = a.SongType == b.SongType ? 0.2 : 0.0;
        var bpm = a.BpmAverage is { } bpmA && b.BpmAverage is { } bpmB
            ? 0.2 * Math.Clamp(1 - Math.Abs(bpmA - bpmB) / 60.0, 0, 1)
            : 0.0;
        var debut = a.DebutMix == b.DebutMix ? 0.1 : 0.0;
        return artist + songType + bpm + debut;
    }
}
