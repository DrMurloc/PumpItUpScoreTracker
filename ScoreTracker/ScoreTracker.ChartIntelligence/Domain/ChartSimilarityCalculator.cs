using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     The settled similarity formula (docs/design/chart-similarity.md). Pure: the saga
///     feeds one (mix, chart-type) pool of features, this returns each chart's top
///     neighbors. Hard gates — level within ±2, never the same song (siblings are
///     navigation, not discovery); the pool itself carries the same-mix/same-type gates.
///     Five signals in [0,1], combined as a weighted **geometric** mean renormalized over
///     whichever signals the pair has; an edge needs at least two non-metadata signals
///     (metadata alone never makes a neighbor), a 0.55 floor, and only the best eight
///     survive. Level distance costs nothing inside the window — see
///     <see cref="LevelWindow" />. Geometric, not arithmetic: an arithmetic
///     mean squares its weights, so a signal at 0.25 weight contributes a sixteenth of its
///     variance and four agreeing signals bury the one that dissents. In log space a low
///     signal drags the whole score down and cannot be outvoted — "alike in every way that
///     matters" rather than "alike on average". Weights are product behavior — tuned by PR
///     after the calibration eyeball, never config.
/// </summary>
internal static class ChartSimilarityCalculator
{
    internal const double SkillWeight = 0.30;
    internal const double DifficultyWeight = 0.25;
    internal const double PlayersWeight = 0.25;
    internal const double IntensityWeight = 0.10;
    internal const double MetaWeight = 0.10;

    /// <summary>NPS, sustain fraction, time-under-tension fraction.</summary>
    internal const int IntensityDimensions = 3;

    /// <summary>
    ///     Applied to badge coverage before the distance, so what a chart is built on
    ///     outweighs what it merely brushes past. The common badges average ~0.3 coverage
    ///     across the corpus — every chart runs, jumps and jacks a bit — and mass
    ///     normalization alone would let that shared baseline argue two charts are alike.
    ///     Squaring keeps a 0.9 badge at 0.81 while a 0.3 drops to 0.09, so a defining
    ///     badge outweighs an incidental one 52:1 instead of 7:1. 1.0 disables it.
    /// </summary>
    internal const double CoverageGamma = 2.0;

    /// <summary>
    ///     How far a chart may reach for neighbours, in folders. A reach limiter, not a
    ///     difficulty statement: the folder level is Andamiro's passing level, applied
    ///     inconsistently, so distance within the window carries no penalty — a chart two
    ///     folders away is as eligible as one in the same folder. What a chart actually
    ///     demands is what Skill reads; how hard it actually is, is what the scoring level
    ///     inside Difficulty reads.
    /// </summary>
    internal const int LevelWindow = 2;

    internal const int MinimumSharedScorers = 30;
    internal const double ScoreFloor = 0.55;
    internal const int TopK = 8;

    /// <summary>
    ///     What a signal of exactly zero is worth to the geometric mean. Players clamps
    ///     negative correlation to zero and Meta bottoms out at zero, and log(0) would take
    ///     the whole score to zero — a veto no single signal has earned. At 0.01 a zeroed
    ///     signal is ruinous in proportion to its weight and nothing more.
    /// </summary>
    internal const double SignalFloor = 0.01;

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

            var skill = SkillSimilarity(a, b);
            var difficulty = DifficultySimilarity(a, b);
            var (players, sharedScorers) = PlayerSimilarity(a, b);
            var intensity = IntensitySimilarity(intensityZ[a.ChartId], intensityZ[b.ChartId]);
            var meta = MetaSimilarity(a, b);

            var nonMetaAvailable = new[] { skill, difficulty, players, intensity }.Count(s => s != null);
            if (nonMetaAvailable < 2) continue;

            var logSum = 0.0;
            var weightTotal = 0.0;
            Fold(skill, SkillWeight);
            Fold(difficulty, DifficultyWeight);
            Fold(players, PlayersWeight);
            Fold(intensity, IntensityWeight);
            Fold(meta, MetaWeight);

            var score = Math.Exp(logSum / weightTotal);
            if (score < ScoreFloor) continue;

            candidates[a.ChartId]
                .Add(new ChartSimilarityEdge(b.ChartId, score, skill, difficulty, players, intensity, meta,
                    sharedScorers));
            candidates[b.ChartId]
                .Add(new ChartSimilarityEdge(a.ChartId, score, skill, difficulty, players, intensity, meta,
                    sharedScorers));
            continue;

            void Fold(double? signal, double weight)
            {
                if (signal == null) return;
                logSum += weight * Math.Log(Math.Max(signal.Value, SignalFloor));
                weightTotal += weight;
            }
        }

        return candidates.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<ChartSimilarityEdge>)kv.Value
                .OrderByDescending(e => e.Score)
                .Take(TopK)
                .ToArray());
    }

    /// <summary>
    ///     Bray-Curtis over gamma-shaped badge coverage: <c>1 − Σ|a−b| / Σ(a+b)</c>.
    ///     Dividing by the pair's own coverage mass rather than by a dimension count is what
    ///     makes the profile's shape decide the score. A badge neither chart carries costs
    ///     nothing on either side of the ratio, so shared absence is free and the sparse
    ///     tail cannot dilute a real gap — which a mean over the union does, and which is
    ///     why a weighted mean cannot fix it either (the weights land in the denominator and
    ///     cancel). A badge one chart is built on and the other never touches contributes
    ///     its whole magnitude to the distance. Missing without banked badges on both sides.
    /// </summary>
    internal static double? SkillSimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        if (a.BadgeCoverage.Count == 0 || b.BadgeCoverage.Count == 0) return null;
        double difference = 0, mass = 0;
        foreach (var badge in a.BadgeCoverage.Keys.Union(b.BadgeCoverage.Keys))
        {
            var coverageA = Shaped(a.BadgeCoverage.GetValueOrDefault(badge));
            var coverageB = Shaped(b.BadgeCoverage.GetValueOrDefault(badge));
            difference += Math.Abs(coverageA - coverageB);
            mass += coverageA + coverageB;
        }

        return mass == 0 ? null : 1 - difference / mass;

        static double Shaped(double coverage)
        {
            return Math.Pow(coverage, CoverageGamma);
        }
    }

    /// <summary>
    ///     Mean of the available difficulty-behavior components: pass-tier distance,
    ///     score-tier distance (both over the 7-step ladder), letter-percentile curve
    ///     distance on the shared grade axis, and continuous scoring-level distance.
    /// </summary>
    private static double? DifficultySimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
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
    ///     spread pins z to 0 (everyone is the cohort norm). Note count is deliberately
    ///     not a dimension: charts are padded toward a per-folder note-count norm, so the
    ///     within-cohort spread is tiny and dividing by it would amplify differences the
    ///     padding was designed to erase — and NPS already carries density.
    /// </summary>
    private static IReadOnlyDictionary<Guid, double?[]> ComputeIntensityZScores(
        IReadOnlyList<ChartSimilarityFeatures> pool)
    {
        var result = pool.ToDictionary(c => c.ChartId, _ => new double?[IntensityDimensions]);
        foreach (var levelGroup in pool.GroupBy(c => c.Level))
        {
            var charts = levelGroup.ToArray();
            for (var dimension = 0; dimension < IntensityDimensions; dimension++)
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
                _ => c.TensionFraction
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
