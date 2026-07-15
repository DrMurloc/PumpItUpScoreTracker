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
///     costs nothing inside the window, see <see cref="LevelWindow" />. Those gates are
///     the only thing that removes a pair: what survives is ranked and the best
///     <see cref="TopK" /> are kept, tail included, with no score bar anywhere.
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

    /// <summary>NPS, sustain fraction, burst fraction — in <see cref="IntensityWeights" /> order.</summary>
    internal const int IntensityDimensions = 3;

    /// <summary>
    ///     The grind and the spikes weigh the same: they are the two halves of one
    ///     decomposition and the taxonomy turns on both ends of it — Gargoyle is all
    ///     sustain, Viyella's is all spikes, neither is the senior partner. NPS trails
    ///     because it is the corroborating axis rather than a verdict: real evidence (it
    ///     saw through Altale's 90 BPM and TRICKL4SH's 220 when nominal BPM had them
    ///     60–75 apart) but the dimension most likely to coincide by accident.
    /// </summary>
    private static readonly double[] IntensityWeights = { 0.20, 0.40, 0.40 };

    /// <summary>
    ///     How many cohort sigma of distance on one dimension takes it from "identical" to
    ///     "nothing in common". Typical |Δz| is ~0.6, so this uses only a fifth of its
    ///     range — provisional, and a one-constant retune once a calibration run says what
    ///     the score distribution actually looks like.
    /// </summary>
    internal const double IntensityK = 3.0;

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
    ///     inconsistently, so distance within the window carries no penalty — a chart one
    ///     folder away is as eligible as one in the same folder. What a chart actually
    ///     demands is what Skill reads; how hard it will be for the viewer is the shelf's
    ///     ordering, not the score's business.
    /// </summary>
    internal const int LevelWindow = 1;

    /// <summary>
    ///     The second half of the reach limiter, in scoring levels, intersected with
    ///     <see cref="LevelWindow" />. The folder alone is too blunt to bound reach with:
    ///     scoring level roams from it by `sd 0.95` and as far as `+3.94`, and **19% of
    ///     charts sit more than a full level off their folder**, so a ±2-folder gate was
    ///     admitting pairs six scoring levels apart. Measured against a Rush-More D23
    ///     anchor, 201 of the 878 charts that gate let through were more than two scoring
    ///     levels away.
    ///     A chart with no scoring level (~13% in the 17–26 range) is gated on the folder
    ///     alone rather than excluded — a missing measurement is not a reason to be
    ///     unreachable.
    /// </summary>
    internal const double ScoringLevelWindow = 1.25;

    /// <summary>
    ///     Neighbors persisted per (mix, chart). Twenty rather than eight, and with no
    ///     score bar of any kind: ~84k rows across the catalog is nothing, and storing the
    ///     tail is what pays for the near-misses shelf, the "closest we could find"
    ///     degraded state, and a floor that can be retuned by redeploy instead of by a
    ///     rebuild. Where the bar falls is the shelf's call, not the graph's.
    /// </summary>
    internal const int TopK = 20;

    /// <summary>How many of a pair's shared badges ride the edge; the shelf renders fewer.</summary>
    internal const int SharedBadgeCount = 5;

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
            if (!WithinReach(a, b)) continue;
            var edge = ScorePair(a, b, intensityZ);
            if (edge == null) continue;

            candidates[a.ChartId].Add(edge with { SimilarChartId = b.ChartId });
            candidates[b.ChartId].Add(edge with { SimilarChartId = a.ChartId });
        }

        return candidates.ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<ChartSimilarityEdge>)kv.Value
                .OrderByDescending(e => e.Score)
                .Take(TopK)
                .ToArray());
    }

    /// <summary>
    ///     One anchor against an explicit target list, best first — the live path behind
    ///     filtered reads and the out-of-window "I liked this D18, what D23s play like it"
    ///     case. **No level window**: the caller has already decided what to compare
    ///     against, and that is the whole point of asking live instead of reading the
    ///     precalculated graph.
    ///     <paramref name="cohort" /> is what intensity z-scores against and must be the
    ///     anchor's full (mix, type) pool, not the filtered targets — a chart's intensity
    ///     is unusual relative to its folder, not relative to whatever the reader happened
    ///     to filter to. Scoring against the filtered set would let a filter change the
    ///     scores.
    /// </summary>
    public static IReadOnlyList<ChartSimilarityEdge> BuildEdgesFor(ChartSimilarityFeatures anchor,
        IReadOnlyList<ChartSimilarityFeatures> targets, IReadOnlyList<ChartSimilarityFeatures> cohort)
    {
        var intensityZ = ComputeIntensityZScores(cohort);
        if (!intensityZ.ContainsKey(anchor.ChartId)) return Array.Empty<ChartSimilarityEdge>();

        return targets
            .Where(t => intensityZ.ContainsKey(t.ChartId))
            .Select(t => ScorePair(anchor, t, intensityZ) is { } edge ? edge with { SimilarChartId = t.ChartId } : null)
            .Where(e => e != null)
            .Select(e => e!)
            .OrderByDescending(e => e.Score)
            .ToArray();
    }

    /// <summary>
    ///     Both halves of the reach limiter, intersected: near enough in the folder AND
    ///     near enough on what the scores actually say. Neither alone is sufficient —
    ///     the folder is inconsistently applied, and scoring level is missing for ~13% of
    ///     charts, so a pair only needs to clear the scoring-level test when both ends
    ///     have one.
    /// </summary>
    internal static bool WithinReach(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        if (Math.Abs(a.Level - b.Level) > LevelWindow) return false;
        if (a.ScoringLevel is { } scoringA && b.ScoringLevel is { } scoringB)
            return Math.Abs(scoringA - scoringB) <= ScoringLevelWindow;
        return true;
    }

    /// <summary>
    ///     The pair's score, or null if they are siblings or either lacks a mandatory
    ///     signal. Both signals are mandatory because they come from the same piucenter
    ///     crawl: in practice a chart has both or neither, and a pair matched on one of
    ///     the two is a pair we know half of, not a neighbor. SimilarChartId is left
    ///     unset — the caller knows which end it is looking from.
    /// </summary>
    private static ChartSimilarityEdge? ScorePair(ChartSimilarityFeatures a, ChartSimilarityFeatures b,
        IReadOnlyDictionary<Guid, double?[]> intensityZ)
    {
        if (a.ChartId == b.ChartId) return null;
        if (a.SongName.Equals(b.SongName)) return null;

        var skill = SkillSimilarity(a, b);
        var intensity = IntensitySimilarity(intensityZ[a.ChartId], intensityZ[b.ChartId]);
        if (skill == null || intensity == null) return null;

        var score = Math.Pow(Math.Max(skill.Score, SignalFloor), SkillWeight)
                    * Math.Pow(Math.Max(intensity.Value, SignalFloor), IntensityWeight);
        return new ChartSimilarityEdge(b.ChartId, score, skill.Score, intensity.Value, skill.SharedBadges);
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
    ///     The shared terms come back with the score because they are the same arithmetic —
    ///     see <see cref="SharedBadgeCoverage" />.
    /// </summary>
    internal static SkillMatch? SkillSimilarity(ChartSimilarityFeatures a, ChartSimilarityFeatures b)
    {
        if (a.BadgeCoverage.Count == 0 || b.BadgeCoverage.Count == 0) return null;
        double difference = 0, mass = 0;
        var shared = new List<SharedBadgeCoverage>();
        foreach (var badge in a.BadgeCoverage.Keys.Union(b.BadgeCoverage.Keys))
        {
            var rawA = a.BadgeCoverage.GetValueOrDefault(badge);
            var rawB = b.BadgeCoverage.GetValueOrDefault(badge);
            var coverageA = Shaped(rawA);
            var coverageB = Shaped(rawB);
            difference += Math.Abs(coverageA - coverageB);
            mass += coverageA + coverageB;
            var overlap = Math.Min(rawA, rawB);
            if (overlap > 0) shared.Add(new SharedBadgeCoverage(badge, overlap));
        }

        if (mass == 0) return null;
        return new SkillMatch(1 - difference / mass,
            shared.OrderByDescending(s => s.Coverage).ThenBy(s => s.Badge, StringComparer.Ordinal)
                .Take(SharedBadgeCount).ToArray());

        static double Shaped(double coverage)
        {
            return Math.Pow(coverage, CoverageGamma);
        }
    }

    /// <summary>The skill signal and the badges it was made of.</summary>
    internal sealed record SkillMatch(double Score, IReadOnlyList<SharedBadgeCoverage> SharedBadges);

    /// <summary>
    ///     Step-analysis scalars z-scored within each chart's own (type, level) cohort —
    ///     the pool is one type, so the cohort is the level group. A cohort with no
    ///     spread pins z to 0 (everyone is the cohort norm).
    ///     **The cohort is here for NPS.** It climbs 7.81 → 14.46 across S15–S25, so its
    ///     variance is dominated by between-folder differences and only the folder's own
    ///     spread is an honest ruler — a corpus-wide sd would flatten a real gap to
    ///     nothing. Sustain and burst do not move with level at all (0.116 and 0.194 sd
    ///     per level step, against NPS's 0.512), so for them this is very nearly a
    ///     division by a constant. That is measured, deliberate, and **not** an invitation
    ///     to give NPS the same treatment — see the two rows in chart-similarity.md §9.
    ///     Z-scores here and absolute
    ///     coverage in skill is deliberate: low intensity is a property (two charts both
    ///     unusually chill for their level genuinely are alike), where low badge coverage
    ///     is absence (two charts both lacking brackets are not thereby alike). Note count
    ///     is deliberately not a dimension: charts are padded toward a per-folder
    ///     note-count norm, so the within-cohort spread is tiny and dividing by it would
    ///     amplify differences the padding was designed to erase — and NPS already carries
    ///     density. Nor is time under tension: sustain is a subset of it, so pairing the
    ///     two counted the grind twice and left the spikes with no dimension at all —
    ///     <see cref="ChartSimilarityFeatures.BurstFraction" /> is the half that was
    ///     missing.
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
                _ => c.BurstFraction
            };
        }
    }

    /// <summary>
    ///     Weighted geometric mean of the per-dimension similarities, renormalized over
    ///     whichever dimensions both charts have. Geometric for the same reason the outer
    ///     combination is: averaging lets the dimensions that agree pay for the one that
    ///     does not. Slapstick Parfait and Horang Pungryuga (D21) run at the same 10.7 NPS
    ///     over near-identical sustain — .109 against .095 — and one of them spends half
    ///     its length bursting (.118 against .516). Averaging called that pair 0.75 and
    ///     shipped it; in log space the dead burst dimension is not something a matching
    ///     NPS can buy back.
    /// </summary>
    private static double? IntensitySimilarity(double?[] zA, double?[] zB)
    {
        var logSum = 0.0;
        var weightTotal = 0.0;
        for (var dimension = 0; dimension < IntensityDimensions; dimension++)
        {
            if (zA[dimension] is not { } a || zB[dimension] is not { } b) continue;
            var similarity = Math.Clamp(1 - Math.Abs(a - b) / IntensityK, 0, 1);
            var weight = IntensityWeights[dimension];
            logSum += weight * Math.Log(Math.Max(similarity, SignalFloor));
            weightTotal += weight;
        }

        return weightTotal == 0 ? null : Math.Exp(logSum / weightTotal);
    }
}
