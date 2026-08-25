using System.Text.Json;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services.ScoreCalculator;

/// <summary>
///     Everything `/PhoenixCalculator/{mix}` renders, computed once per request
///     (docs/design/phoenix-score-calculator.md). Every number the page prints comes from the
///     engine at render — grade floors from <see cref="PhoenixLetterGradeHelperMethods" />,
///     judgement costs and budgets from <see cref="ScoreScreen" /> itself — so the page cannot
///     drift from the formula it explains (D2/D3). The measured sections (population, spreads,
///     holds) arrive as query results and are shaped here.
/// </summary>
public sealed class ScoreCalculatorModel
{
    /// <summary>The chart sizes the judgement-cost section offers; the middle default renders server-side.</summary>
    public static readonly int[] CostChartSizes = { 500, 1000, 1500, 2000 };

    public const int DefaultCostChartSize = 1000;

    /// <summary>Grade bands under this many bests stay out of the spread table (D8).</summary>
    public const int MinSpreadPlays = 50;

    /// <summary>Levels under this many bests stay off the population chart (D9).</summary>
    public const int MinPopulationBests = 20;

    private ScoreCalculatorModel(MixEnum mix, IReadOnlyList<GradeRung> ladder,
        IReadOnlyList<GradeJudgementSpread> spreads, IReadOnlyList<PopulationLevel> population,
        long populationTotal, IReadOnlyDictionary<ChartType, IReadOnlyList<NoteCountLevel>> noteCounts,
        HoldTickProfile holds, string constantsJson)
    {
        Mix = mix;
        Ladder = ladder;
        Spreads = spreads;
        Population = population;
        PopulationTotal = populationTotal;
        NoteCounts = noteCounts;
        Holds = holds;
        ConstantsJson = constantsJson;
    }

    public MixEnum Mix { get; }

    public MixEnum OtherMix => Mix == MixEnum.Phoenix2 ? MixEnum.Phoenix : MixEnum.Phoenix2;

    /// <summary>This mix's grade ladder, best first, each rung carrying the other mix's floor.</summary>
    public IReadOnlyList<GradeRung> Ladder { get; }

    /// <summary>Measured spreads, best grade first, display-gated (D8).</summary>
    public IReadOnlyList<GradeJudgementSpread> Spreads { get; }

    /// <summary>Levels with enough bests to draw, ascending, bands merged to the grade metals (D9).</summary>
    public IReadOnlyList<PopulationLevel> Population { get; }

    public long PopulationTotal { get; }

    /// <summary>Note-count spreads per type, levels ascending (D10). Types absent when unmeasured.</summary>
    public IReadOnlyDictionary<ChartType, IReadOnlyList<NoteCountLevel>> NoteCounts { get; }

    public HoldTickProfile Holds { get; }

    /// <summary>
    ///     The constants block the page's script computes from: both mixes' floors, the
    ///     judgement weights, and the owner-verified calorie table — emitted, never retyped.
    /// </summary>
    public string ConstantsJson { get; }

    public static ScoreCalculatorModel For(MixEnum mix, IEnumerable<Chart> charts,
        IEnumerable<Chart> phoenixFallbackCharts, IReadOnlyList<LevelScorePopulation> population,
        IReadOnlyList<GradeJudgementSpread> spreads, HoldTickProfile holds)
    {
        var chartArray = charts.Where(c => c.Type is ChartType.Single or ChartType.Double).ToArray();
        var fallbackCounts = mix == MixEnum.Phoenix
            ? new Dictionary<Guid, int>()
            : phoenixFallbackCharts.Where(c => c.NoteCount is > 0)
                .ToDictionary(c => c.Id, c => c.NoteCount!.Value);

        return new ScoreCalculatorModel(
            mix,
            BuildLadder(mix),
            spreads.Where(s => s.Plays >= MinSpreadPlays).ToArray(),
            BuildPopulation(mix, population),
            population.Sum(p => (long)p.Total),
            BuildNoteCounts(chartArray, fallbackCounts),
            holds,
            BuildConstantsJson());
    }

    // ── the ladder ─────────────────────────────────────────────────────────────

    public sealed record GradeRung(PhoenixLetterGrade Grade, int Floor, int Top, int OtherFloor);

    private static IReadOnlyList<GradeRung> BuildLadder(MixEnum mix)
    {
        var other = mix == MixEnum.Phoenix2 ? MixEnum.Phoenix : MixEnum.Phoenix2;
        return Enum.GetValues<PhoenixLetterGrade>()
            .OrderByDescending(g => (int)g.GetMinimumScoreFor(mix))
            .Select(g => new GradeRung(g, g.GetMinimumScoreFor(mix), g.GetMaximumScoreFor(mix),
                g.GetMinimumScoreFor(other)))
            .ToArray();
    }

    /// <summary>The letters below AAA moved between the mixes; the ladder calls each rung out.</summary>
    public static readonly PhoenixLetterGrade[] MovedGrades =
        Enum.GetValues<PhoenixLetterGrade>()
            .Where(g => (int)g.GetMinimumScoreFor(MixEnum.Phoenix) !=
                        (int)g.GetMinimumScoreFor(MixEnum.Phoenix2))
            .OrderByDescending(g => (int)g.GetMinimumScoreFor(MixEnum.Phoenix2))
            .ToArray();

    // ── judgement costs, from the engine itself ────────────────────────────────

    /// <summary>What one of the judgement costs on a chart of <paramref name="notes" />, best case.</summary>
    public static int CostOfOneGreat(int notes)
    {
        return 1_000_000 - new ScoreScreen(notes - 1, 1, 0, 0, 0, notes).CalculatePhoenixScore;
    }

    public static int CostOfOneGood(int notes)
    {
        // A good holds the run without advancing it: max combo tops out one short.
        return 1_000_000 - new ScoreScreen(notes - 1, 0, 1, 0, 0, notes - 1).CalculatePhoenixScore;
    }

    public static int CostOfOneBad(int notes)
    {
        return 1_000_000 - new ScoreScreen(notes - 1, 0, 0, 1, 0, notes - 1).CalculatePhoenixScore;
    }

    /// <summary>A miss at the chart's edge — the run barely notices.</summary>
    public static int CostOfOneMissAtTheEdge(int notes)
    {
        return 1_000_000 - new ScoreScreen(notes - 1, 0, 0, 0, 1, notes - 1).CalculatePhoenixScore;
    }

    /// <summary>The same miss dead-centre, where it halves the run.</summary>
    public static int CostOfOneMissMidCombo(int notes)
    {
        return 1_000_000 - new ScoreScreen(notes - 1, 0, 0, 0, 1, (notes - 1) / 2).CalculatePhoenixScore;
    }

    /// <summary>
    ///     The most greats a grade tolerates on a chart of <paramref name="notes" /> when
    ///     everything else is perfect — greats keep the combo running, so the search only
    ///     moves the accuracy term. Solved against the real formula, not a closed form.
    /// </summary>
    public static int GreatsAllowedFor(PhoenixLetterGrade grade, MixEnum mix, int notes)
    {
        var floor = (int)grade.GetMinimumScoreFor(mix);
        var low = 0;
        var high = notes;
        while (low < high)
        {
            var candidate = (low + high + 1) / 2;
            if ((int)new ScoreScreen(notes - candidate, candidate, 0, 0, 0, notes).CalculatePhoenixScore >= floor)
                low = candidate;
            else
                high = candidate - 1;
        }

        return low;
    }

    /// <summary>The budget table's rungs: every grade from the top down to this mix's 900,000 rung.</summary>
    public IEnumerable<GradeRung> BudgetRungs => Ladder.Where(r => r.Floor >= 900_000);

    // ── the population, banded by grade metal (D9) ─────────────────────────────

    /// <summary>
    ///     One stacked band: a score range, the count in it, and the letters it covers on this
    ///     mix — sixteen grades share the metal ladder, so the chart draws five bands rather
    ///     than sixteen slivers (the folder-spectrum precedent).
    /// </summary>
    public sealed record PopulationBand(string Key, int Count, string CoveredGrades);

    public sealed record PopulationLevel(int Level, int Total, IReadOnlyList<PopulationBand> Bands);

    private static readonly (string Key, int Floor, int Ceiling)[] MetalBands =
    {
        ("below", 0, 900_000),
        ("copper", 900_000, 950_000),
        ("silver", 950_000, 970_000),
        ("gold", 970_000, 990_000),
        ("ice", 990_000, 1_000_001)
    };

    private static IReadOnlyList<PopulationLevel> BuildPopulation(MixEnum mix,
        IReadOnlyList<LevelScorePopulation> population)
    {
        return population
            .Where(level => level.Total >= MinPopulationBests)
            .OrderBy(level => level.Level)
            .Select(level => new PopulationLevel(level.Level, level.Total, MetalBands
                .Select(band => new PopulationBand(band.Key, CountIn(level, band.Key),
                    CoveredGrades(mix, band.Floor, band.Ceiling)))
                .ToArray()))
            .ToArray();
    }

    private static int CountIn(LevelScorePopulation level, string bandKey)
    {
        return bandKey switch
        {
            "below" => level.Below900k,
            "copper" => level.From900k,
            "silver" => level.From950k,
            "gold" => level.From970k + level.From980k,
            _ => level.From990k + level.From995k
        };
    }

    /// <summary>
    ///     The letters whose floor lands inside the band on this mix, best first — the legend's
    ///     labels. The below-the-line band reads as its topmost letter "and under".
    /// </summary>
    public static string CoveredGrades(MixEnum mix, int floor, int ceiling)
    {
        var covered = Enum.GetValues<PhoenixLetterGrade>()
            .Where(g => (int)g.GetMinimumScoreFor(mix) >= floor && (int)g.GetMinimumScoreFor(mix) < ceiling)
            .OrderByDescending(g => (int)g.GetMinimumScoreFor(mix))
            .Select(g => g.GetName())
            .ToArray();
        return string.Join(" · ", covered);
    }

    public static string TopGradeBelowTheLine(MixEnum mix)
    {
        return Enum.GetValues<PhoenixLetterGrade>()
            .Where(g => (int)g.GetMinimumScoreFor(mix) < 900_000)
            .OrderByDescending(g => (int)g.GetMinimumScoreFor(mix))
            .First()
            .GetName();
    }

    // ── note counts (D10) ──────────────────────────────────────────────────────

    public sealed record NoteCountLevel(int Level, int Charts, int Min, int P10, int Median, int P90, int Max);

    private static IReadOnlyDictionary<ChartType, IReadOnlyList<NoteCountLevel>> BuildNoteCounts(
        IReadOnlyList<Chart> charts, IReadOnlyDictionary<Guid, int> fallbackCounts)
    {
        return charts
            .Select(chart => (chart.Type, Level: (int)chart.Level,
                Count: chart.NoteCount
                       ?? (fallbackCounts.TryGetValue(chart.Id, out var fallback) ? fallback : (int?)null)))
            .Where(c => c.Count is > 0)
            .GroupBy(c => c.Type)
            .ToDictionary(
                type => type.Key,
                type => (IReadOnlyList<NoteCountLevel>)type
                    .GroupBy(c => c.Level)
                    .OrderBy(level => level.Key)
                    .Select(level =>
                    {
                        var counts = level.Select(c => c.Count!.Value).OrderBy(v => v).ToArray();
                        return new NoteCountLevel(level.Key, counts.Length, counts[0],
                            Quantile(counts, .1), Quantile(counts, .5), Quantile(counts, .9), counts[^1]);
                    })
                    .ToArray());
    }

    private static int Quantile(int[] sortedValues, double quantile)
    {
        var index = (int)Math.Round(quantile * (sortedValues.Length - 1), MidpointRounding.AwayFromZero);
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    // ── the constants the script reads (D2) ────────────────────────────────────

    /// <summary>
    ///     The same constants block, for the Score Breakdown Dialog's engine — one emission
    ///     feeding both of score-breakdown.js's surfaces, so they cannot disagree (§7.3 of the
    ///     session-breakdown design). Static because nothing in it varies per request.
    /// </summary>
    public static string EngineConstantsJson { get; } = BuildConstantsJson();

    private static string BuildConstantsJson()
    {
        return JsonSerializer.Serialize(new
        {
            weights = new { perfect = 1.0, great = .6, good = .2, bad = .1, combo = .005, accuracy = .995 },
            // The site's letter, broken-letter and plate art, spelled by ShareCardImages — the
            // script renders images, never chips, and never builds a URL of its own.
            gradeImages = Enum.GetValues<PhoenixLetterGrade>()
                .ToDictionary(g => g.GetName(), g => ShareCardImages.LetterGrade(g, false)),
            gradeImagesBroken = Enum.GetValues<PhoenixLetterGrade>()
                .ToDictionary(g => g.GetName(), g => ShareCardImages.LetterGrade(g, true)),
            plateImages = Enum.GetValues<PhoenixPlate>()
                .ToDictionary(p => p.GetName(), p => ShareCardImages.Plate(p)),
            floors = new[] { MixEnum.Phoenix, MixEnum.Phoenix2 }.ToDictionary(
                m => m.ToString(),
                m => Enum.GetValues<PhoenixLetterGrade>()
                    .OrderByDescending(g => (int)g.GetMinimumScoreFor(m))
                    .Select(g => new { grade = g.GetName(), floor = (int)g.GetMinimumScoreFor(m) })
                    .ToArray()),
            calorieThresholds = ScoreScreen.EstimatedNoteCountThresholds
                .OrderBy(kv => kv.Key)
                .Select(kv => new[] { kv.Key, kv.Value })
                .ToArray()
        });
    }
}
