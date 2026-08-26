using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     The rule-based facet engine (docs/design/chart-verdicts.md). Pure: evidence in,
///     fired facets out, salience order (headline candidates first, Population always
///     last). Every facet has a minimum-evidence bar — below it the facet is omitted,
///     never hedged. Facts only; the localized sentences are Web's job.
/// </summary>
internal static class ChartVerdictService
{
    /// <summary>Passes needed before the pass band means anything.</summary>
    internal const int PassBandMinimumPasses = 20;

    /// <summary>The yield knee: where the population's average crosses SS+ (975k).</summary>
    internal const int YieldKneeScore = 975_000;

    /// <summary>
    ///     Passers a competitive-level bucket needs before its average may be read as the
    ///     knee. Without it the knee is whatever the *lowest* level with a good score
    ///     happens to be, and one player is enough to be a level's whole population — an
    ///     S20 with a single competitive-level-12 passer reported that scores open up at
    ///     12. It also filters the unrated: ~900 accounts carry a competitive level of 1.0
    ///     or below (a "not enough data" floor, not a skill), and they scatter a passer or
    ///     two across charts far above them.
    ///     A healthy bucket in the range that matters runs 15–90 passers, so ten is a low
    ///     bar that still demands a population. The knee only ever moves later, never
    ///     earlier — this cannot invent a knee, only decline to believe one.
    ///     The number is published (<see cref="ChartEvidenceThresholds" />) because the graph
    ///     this sentence captions is drawn on the far side of the vertical boundary and has
    ///     to hold the same bar; it did not, and the two contradicted each other on the page.
    /// </summary>
    internal const int YieldKneeMinimumPassesPerLevel = ChartEvidenceThresholds.MinimumPerCompetitiveLevel;

    /// <summary>An adjacent percentile jump this big is a letter wall.</summary>
    internal const double LetterWallMinimumJump = 0.25;

    /// <summary>Plated clears needed before the plate-residual verdict may speak.</summary>
    internal const int PlateResidualMinimumClears = 50;

    /// <summary>Segment coverage a skill needs to count as a fingerprint headline.</summary>
    internal const double FingerprintMinimumCoverage = 0.25;

    /// <summary>Time-under-tension share that flags a chart as sustained.</summary>
    internal const double SustainedTensionFraction = 0.6;

    /// <summary>
    ///     How far over its printed level a crux must run before it is worth a sentence. The
    ///     same bar the identity chips draw a spike at, so the page and the verdict cannot
    ///     disagree about whether a chart has one.
    /// </summary>
    internal const double CruxMinimumPeakiness = 0.7;

    public static IReadOnlyList<ChartVerdictFacet> ComputeFacets(ChartVerdictInputs inputs)
    {
        var facets = new List<ChartVerdictFacet>();
        // Salience order per the design doc: PassVsScore → LetterWall → YieldKnee →
        // StyleFingerprint carry headlines; the rest are supporting; Population closes.
        if (PassVsScore(inputs) is { } passVsScore) facets.Add(passVsScore);
        if (LetterWall(inputs) is { } wall) facets.Add(wall);
        if (YieldKnee(inputs) is { } knee) facets.Add(knee);
        if (StyleFingerprint(inputs) is { } style) facets.Add(style);
        // The crux sits with the fingerprint: one says what the chart is made of, the other
        // what its hardest stretch is made of, and they disagree on most charts.
        if (Crux(inputs) is { } crux) facets.Add(crux);
        if (PassBand(inputs) is { } band) facets.Add(band);
        if (PlateResidual(inputs) is { } plates) facets.Add(plates);
        if (History(inputs) is { } history) facets.Add(history);
        facets.Add(new PopulationVerdict(inputs.ScoresTracked,
            inputs.ScoresTracked == 0 ? 0 : (double)inputs.PassCount / inputs.ScoresTracked));
        return facets;
    }

    private static PassVsScoreVerdict? PassVsScore(ChartVerdictInputs inputs)
    {
        if (Recorded(inputs.PassTier) is not { } pass || Recorded(inputs.ScoreTier) is not { } score)
            return null;
        return pass == TierListCategory.Medium && score == TierListCategory.Medium
            ? null
            : new PassVsScoreVerdict(pass, score);

        static TierListCategory? Recorded(TierListCategory? tier)
        {
            return tier is null or TierListCategory.Unrecorded ? null : tier;
        }
    }

    private static LetterWallVerdict? LetterWall(ChartVerdictInputs inputs)
    {
        if (inputs.LetterPercentiles == null || inputs.LetterPercentiles.Count < 2) return null;
        var ordered = inputs.LetterPercentiles.OrderBy(kv => kv.Key).ToArray();
        var wallGrade = default(ParagonLevel);
        var biggestJump = 0.0;
        for (var i = 1; i < ordered.Length; i++)
        {
            var jump = ordered[i].Value - ordered[i - 1].Value;
            if (jump <= biggestJump) continue;
            biggestJump = jump;
            wallGrade = ordered[i].Key;
        }

        return biggestJump >= LetterWallMinimumJump ? new LetterWallVerdict(wallGrade, biggestJump) : null;
    }

    private static YieldKneeVerdict? YieldKnee(ChartVerdictInputs inputs)
    {
        // Only levels a real population reached: an average over one or two players is a
        // fact about those players, and this facet reads the FIRST level to cross, so the
        // thinnest bucket on the curve is exactly the one that gets believed.
        var populated = inputs.PassCountByLevel
            .Where(l => l.Passes >= YieldKneeMinimumPassesPerLevel)
            .Select(l => l.Level)
            .ToHashSet();
        // The crossing must happen inside the observed range — a curve that starts
        // above (free for everyone we can see) or never arrives has no knee to report.
        var ordered = inputs.ScoreAverageByLevel
            .Where(l => populated.Contains(l.Level))
            .OrderBy(l => l.Level)
            .ToArray();
        if (ordered.Length < 2) return null;
        if (ordered[0].AverageScore >= YieldKneeScore) return null;
        var knee = ordered.FirstOrDefault(l => l.AverageScore >= YieldKneeScore);
        return knee == null ? null : new YieldKneeVerdict(knee.Level);
    }

    private static StyleFingerprintVerdict? StyleFingerprint(ChartVerdictInputs inputs)
    {
        var top = inputs.BadgeWeights.Values
            .Where(b => b.Coverage >= FingerprintMinimumCoverage)
            .OrderByDescending(b => b.Coverage)
            .ThenBy(b => b.Badge, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Select(b => new BadgeCoverageRecord(b.Badge, b.DisplayName, b.Coverage))
            .ToArray();
        if (top.Length == 0) return null;
        return new StyleFingerprintVerdict(top,
            inputs.TensionFraction is { } tension && tension >= SustainedTensionFraction);
    }

    /// <summary>
    ///     What ends you, and where. Silent unless the crux runs clear of the printed level:
    ///     a chart whose peak is its own average has no single stretch worth naming, and
    ///     saying one anyway would put a crux on every chart on the site.
    /// </summary>
    private static CruxVerdict? Crux(ChartVerdictInputs inputs)
    {
        if (inputs.Crux is not { Peakiness: { } peakiness } crux) return null;
        if (peakiness < CruxMinimumPeakiness || crux.Badges.Count == 0) return null;
        return new CruxVerdict(
            crux.Badges.Take(2).Select(b => new BadgeCoverageRecord(b.Badge, b.DisplayName, b.Coverage)).ToArray(),
            peakiness, crux.Position, crux.DurationSeconds);
    }

    private static PassBandVerdict? PassBand(ChartVerdictInputs inputs)
    {
        var totalPasses = inputs.PassCountByLevel.Sum(l => l.Passes);
        if (totalPasses < PassBandMinimumPasses) return null;
        var passerLevels = inputs.PassCountByLevel
            .OrderBy(l => l.Level)
            .SelectMany(l => Enumerable.Repeat(l.Level, l.Passes))
            .ToArray();
        var lower = passerLevels[(passerLevels.Length - 1) / 4];
        var upper = passerLevels[(passerLevels.Length - 1) * 3 / 4];
        return new PassBandVerdict(lower, upper);
    }

    private static PlateResidualVerdict? PlateResidual(ChartVerdictInputs inputs)
    {
        // The binding plate constraint (chart-verdicts.md): plates mostly restate scores,
        // so the ONLY plate verdict is the residual against ExpectedPlateForScore. Lower
        // median (sorted[(n−1)/2]) keeps both medians deterministic and conservative.
        if (inputs.ClearPlates.Count < PlateResidualMinimumClears || inputs.MedianClearScore is not { } median)
            return null;
        var plates = inputs.ClearPlates.OrderBy(p => p).ToArray();
        var medianPlate = plates[(plates.Length - 1) / 2];
        var expected = ScoringConfiguration.ExpectedPlateForScore(median);
        var steps = (int)medianPlate - (int)expected;
        return Math.Abs(steps) < 1 ? null : new PlateResidualVerdict(steps);
    }

    private static HistoryVerdict? History(ChartVerdictInputs inputs)
    {
        if (inputs.MixLevels.Count == 0) return null;
        var levelsChanged = inputs.MixLevels.Select(l => l.Level).Distinct().Count() > 1;
        if (inputs.DebutMix == inputs.CurrentMix && !levelsChanged) return null;
        return new HistoryVerdict(inputs.DebutMix,
            inputs.MixLevels.Select(l => new MixLevelRecord(l.Mix, l.Level)).ToArray());
    }
}
