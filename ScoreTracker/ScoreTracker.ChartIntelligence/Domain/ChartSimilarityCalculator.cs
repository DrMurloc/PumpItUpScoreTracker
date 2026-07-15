namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     The settled similarity formula (docs/design/chart-similarity.md). Pure: the saga
///     feeds one (mix, chart-type) pool of features, this returns each chart's top
///     neighbors. Similarity answers one question — do these two charts pose the same
///     kind of problem — so it reads only what a chart is made of: skill (what it asks
///     of you) and intensity (how hard it asks). How hard it will be *for you* is
///     difficulty, which orders the shelf at read time; who made it and when is metadata,
///     which filters it. Neither is part of the score: averaging a chart property with a
///     viewer property is what flattened the old five-signal formula to sd = 0.030 across
///     a folder.
///     Hard gates — level within ±2, never the same song (siblings are navigation, not
///     discovery); the pool itself carries the same-mix/same-type gates. Level distance
///     costs nothing inside the window, see <see cref="LevelWindow" />.
///     The two signals combine as a weighted **geometric** mean. Geometric, not
///     arithmetic: an arithmetic mean squares its weights, so a light signal contributes
///     a fraction of its variance and the heavy one buries it. In log space a low signal
///     drags the whole score down and cannot be outvoted — "alike in every way that
///     matters" rather than "alike on average". Weights are product behavior — tuned by
///     PR after the calibration eyeball, never config.
/// </summary>
internal static class ChartSimilarityCalculator
{
    /// <summary>
    ///     What a chart asks of you against how hard it asks — 3:1. Both signals are
    ///     mandatory and the weights sum to 1, so the score is simply
    ///     <c>S_skill^0.75 · S_intensity^0.25</c>: no renormalization, nothing to
    ///     average over. Skill leads because two charts that demand different things are
    ///     not alike however similarly they exhaust you.
    /// </summary>
    internal const double SkillWeight = 0.75;

    internal const double IntensityWeight = 0.25;

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
    ///     demands is what Skill reads; how hard it will be for the viewer is the shelf's
    ///     ordering, not the score's business.
    /// </summary>
    internal const int LevelWindow = 2;

    internal const double ScoreFloor = 0.55;
    internal const int TopK = 8;

    /// <summary>
    ///     What a signal of exactly zero is worth to the geometric mean. Bray-Curtis
    ///     returns zero for profiles sharing no coverage at all, and intensity clamps to
    ///     zero at K sigma apart; log(0) would take the score to negative infinity and
    ///     leave every such pair tied at the bottom, unrankable. At 0.01 a zeroed signal
    ///     is ruinous in proportion to its weight and nothing more.
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
            if (Math.Abs(a.Level - b.Level) > LevelWindow) continue;
            if (a.SongName.Equals(b.SongName)) continue;

            // Both signals are mandatory: they come from the same piucenter crawl, so in
            // practice a chart has both or neither, and a pair matched on one of the two
            // is a pair we know half of — not a neighbor.
            var skill = SkillSimilarity(a, b);
            var intensity = IntensitySimilarity(intensityZ[a.ChartId], intensityZ[b.ChartId]);
            if (skill == null || intensity == null) continue;

            var score = Math.Pow(Math.Max(skill.Value, SignalFloor), SkillWeight)
                        * Math.Pow(Math.Max(intensity.Value, SignalFloor), IntensityWeight);
            if (score < ScoreFloor) continue;

            candidates[a.ChartId].Add(new ChartSimilarityEdge(b.ChartId, score, skill.Value, intensity.Value));
            candidates[b.ChartId].Add(new ChartSimilarityEdge(a.ChartId, score, skill.Value, intensity.Value));
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
    ///     Step-analysis scalars z-scored within each chart's own (type, level) cohort —
    ///     the pool is one type, so the cohort is the level group. A cohort with no
    ///     spread pins z to 0 (everyone is the cohort norm). Z-scores here and absolute
    ///     coverage in skill is deliberate: low intensity is a property (two charts both
    ///     unusually chill for their level genuinely are alike), where low badge coverage
    ///     is absence (two charts both lacking brackets are not thereby alike). Note count
    ///     is deliberately not a dimension: charts are padded toward a per-folder
    ///     note-count norm, so the within-cohort spread is tiny and dividing by it would
    ///     amplify differences the padding was designed to erase — and NPS already carries
    ///     density.
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
        var deltas = new List<double>(IntensityDimensions);
        for (var dimension = 0; dimension < zA.Length; dimension++)
            if (zA[dimension] is { } a && zB[dimension] is { } b)
                deltas.Add(Math.Abs(a - b));
        if (deltas.Count == 0) return null;
        return Math.Clamp(1 - deltas.Average() / 3.0, 0, 1);
    }
}
