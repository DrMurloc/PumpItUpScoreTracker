using System.Text.Json;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Services.PumbilityCalculator;

/// <summary>
///     Everything `/PumbilityCalculator/{mix}` renders, computed once per request from the mix's
///     own <see cref="ScoringConfiguration" /> (docs/design/pumbility-calculator.md D3). The page
///     and its section components read this and format; nothing numeric lives in markup, so the
///     page cannot drift from the formula it explains. Pure and synchronous — the only outside
///     input is the chart count per level and type, which the page passes in.
/// </summary>
public sealed class PumbilityCalculatorModel
{
    /// <summary>The value ramp's number of steps (`pc-ramp-0` … `pc-ramp-9`).</summary>
    public const int RampSteps = 10;

    /// <summary>The Phoenix formula prices CO-OP at a flat 2,000 base — shown, and marked as never counted.</summary>
    public const int PhoenixCoOpBase = 2000;

    /// <summary>Levels below this price at zero on both formulas and are not rows.</summary>
    public const int LowestPricedLevel = 10;

    private static readonly PhoenixLetterGrade[] Grades = Enum.GetValues<PhoenixLetterGrade>();
    private static readonly PhoenixPlate[] Plates = Enum.GetValues<PhoenixPlate>();

    private PumbilityCalculatorModel(MixEnum mix, ScoringConfiguration scoring, PhoenixLetterGrade anchor,
        IReadOnlyList<PumbilityCalculatorTypeView> types, PumbilityGradeTable grades, PumbilityPlateTable? plates,
        IReadOnlyList<int> unreadLevels, IReadOnlyList<PumbilityWorkedExample> examples, int coOpChartCount)
    {
        CoOpChartCount = coOpChartCount;
        Mix = mix;
        Scoring = scoring;
        AnchorGrade = anchor;
        Types = types;
        GradeTable = grades;
        PlateTable = plates;
        UnreadLevels = unreadLevels;
        Examples = examples;
    }

    public MixEnum Mix { get; }
    public ScoringConfiguration Scoring { get; }

    /// <summary>The grade whose floor is 900,000 on this mix — the ruler's baseline (D4).</summary>
    public PhoenixLetterGrade AnchorGrade { get; }

    /// <summary>Phoenix 2 prices Singles and Doubles differently, so it renders both; Phoenix renders one table for both.</summary>
    public bool HasTypes => Types.Count > 1;

    /// <summary>Whether the plate enters the formula at all (Phoenix 2) or every plate is ×1.0 (Phoenix).</summary>
    public bool HasPlates => PlateTable != null;

    public IReadOnlyList<PumbilityCalculatorTypeView> Types { get; }
    public PumbilityGradeTable GradeTable { get; }
    public PumbilityPlateTable? PlateTable { get; }

    /// <summary>Levels whose base is extrapolated rather than read — the one footnote the page carries (D12).</summary>
    public IReadOnlyList<int> UnreadLevels { get; }

    /// <summary>The worked examples under the formula: real arithmetic, four combos.</summary>
    public IReadOnlyList<PumbilityWorkedExample> Examples { get; }

    /// <summary>How many CO-OP charts the catalog holds — the Phoenix table's flat-2,000 row (D13).</summary>
    public int CoOpChartCount { get; }

    public PumbilityCalculatorTypeView this[ChartType type] => Types.First(t => t.Type == type);

    /// <summary>
    ///     Builds the model. <paramref name="chartCounts" /> is how many charts of each (type, level)
    ///     the mix's catalog holds — the value table's last column; a missing entry reads as zero.
    /// </summary>
    public static PumbilityCalculatorModel For(MixEnum mix,
        IReadOnlyDictionary<(ChartType Type, int Level), int> chartCounts)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var anchor = PumbilityLevelEquivalence.AnchorGrade(mix);
        var typesToRender = mix == MixEnum.Phoenix2
            ? new[] { ChartType.Single, ChartType.Double }
            : new[] { ChartType.Single };
        var unread = mix == MixEnum.Phoenix2 ? new[] { 28, 29 } : Array.Empty<int>();
        var types = typesToRender.Select(t => BuildType(mix, scoring, t, anchor, chartCounts, unread)).ToArray();

        var grades = new PumbilityGradeTable(
            Grades.ToDictionary(g => g, g => scoring.LetterGradeModifierFor(g, ChartType.Double)),
            Grades.Where(g => scoring.LetterGradeModifierFor(g, ChartType.Single) !=
                              scoring.LetterGradeModifierFor(g, ChartType.Double))
                .ToDictionary(g => g, g => scoring.LetterGradeModifierFor(g, ChartType.Single)),
            Grades.ToDictionary(g => g, g => (int)g.GetMinimumScoreFor(mix)));

        // Every Phoenix plate carries ×1.0 — the formula has no plate term, and the page says so
        // rather than printing a column of ones.
        var plates = mix == MixEnum.Phoenix2
            ? new PumbilityPlateTable(
                Plates.ToDictionary(p => p, p => scoring.PlateModifierFor(p, ChartType.Double)),
                Plates.Where(p => scoring.PlateModifierFor(p, ChartType.Single) !=
                                  scoring.PlateModifierFor(p, ChartType.Double))
                    .ToDictionary(p => p, p => scoring.PlateModifierFor(p, ChartType.Single)))
            : null;

        return new PumbilityCalculatorModel(mix, scoring, anchor, types, grades, plates, unread,
            BuildExamples(mix, scoring), chartCounts.Where(kv => kv.Key.Type == ChartType.CoOp).Sum(kv => kv.Value));
    }

    private static PumbilityCalculatorTypeView BuildType(MixEnum mix, ScoringConfiguration scoring, ChartType type,
        PhoenixLetterGrade anchor, IReadOnlyDictionary<(ChartType, int), int> chartCounts, int[] unread)
    {
        // Rows run from the highest level the catalog holds for this type down to 10 (the top
        // extends to the last level with a chart, so a Singles table stops where Singles stop).
        var top = chartCounts.Where(kv => kv.Key.Item1 is ChartType.Single or ChartType.Double)
            .Where(kv => kv.Key.Item1 == type || mix != MixEnum.Phoenix2)
            .Select(kv => kv.Key.Item2).DefaultIfEmpty((int)DifficultyLevel.Max).Max();
        top = Math.Clamp(top, LowestPricedLevel, (int)DifficultyLevel.Max);

        var anchorIx = Array.IndexOf(Grades, anchor);
        var rulerFrom = Math.Max(0, anchorIx - 2);
        var rows = new List<PumbilityLevelRow>();
        for (var level = top; level >= LowestPricedLevel; level--)
        {
            var difficulty = DifficultyLevel.From(level);
            var cells = Grades.Select(g =>
            {
                var value = PumbilityLevelEquivalence.ValueAt(scoring, type, difficulty, g);
                return new PumbilityValueCell(g, value, PumbilityLevelEquivalence.EquivalentLevel(scoring, type, value));
            }).ToArray();
            var points = cells.Skip(rulerFrom)
                .Select(c => new PumbilityRulerPoint(c.Grade, c.EquivalentLevel, Array.IndexOf(Grades, c.Grade) <= anchorIx))
                .ToArray();
            var count = mix == MixEnum.Phoenix2
                ? chartCounts.GetValueOrDefault((type, level))
                : chartCounts.Where(kv => kv.Key.Item2 == level && kv.Key.Item1 is ChartType.Single or ChartType.Double)
                    .Sum(kv => kv.Value);
            // What the formula multiplies for this type: a Phoenix 2 Single pays Base(level + 1).
            var pricedBase = mix == MixEnum.Phoenix2
                ? ScoringConfiguration.Phoenix2PricedBase(type, difficulty)
                : difficulty.BaseRating;
            rows.Add(new PumbilityLevelRow(level, pricedBase, count, cells, points, unread.Contains(level)));
        }

        // The ruler axis spans the lowest drawn point to the highest, on whole levels, so both
        // the ruler and the table's colour ramp share one scale per type.
        var axisMin = (int)Math.Floor(rows.SelectMany(r => r.RulerPoints).Min(p => p.EquivalentLevel));
        var axisMax = (int)Math.Ceiling(rows.SelectMany(r => r.RulerPoints).Max(p => p.EquivalentLevel));
        if (axisMax <= axisMin) axisMax = axisMin + 1;

        return new PumbilityCalculatorTypeView(type, rows, axisMin, axisMax,
            BuildConstantsJson(mix, scoring, type, anchor, rows));
    }

    /// <summary>
    ///     The worked examples under the formula: real arithmetic, four combos, both types where the
    ///     mix prices them apart. A perfect 1,000,000 keeps SSS+'s multiplier and adds the Perfect
    ///     Game plate — the example that shows PG is a plate, not a grade.
    /// </summary>
    private static IReadOnlyList<PumbilityWorkedExample> BuildExamples(MixEnum mix, ScoringConfiguration scoring)
    {
        var picks = new (ChartType Type, int Level, PhoenixLetterGrade Grade, PhoenixPlate Plate)[]
        {
            (ChartType.Double, 24, PhoenixLetterGrade.S, PhoenixPlate.MarvelousGame),
            (ChartType.Single, 17, PhoenixLetterGrade.AA, PhoenixPlate.FairGame),
            (ChartType.Single, 22, PhoenixLetterGrade.SSSPlus, PhoenixPlate.PerfectGame),
            (ChartType.Double, 10, PhoenixLetterGrade.APlus, PhoenixPlate.RoughGame)
        };
        return picks.Select(p =>
        {
            var level = DifficultyLevel.From(p.Level);
            var score = p.Grade == PhoenixLetterGrade.SSSPlus && p.Plate == PhoenixPlate.PerfectGame
                ? (PhoenixScore)1_000_000
                : p.Grade.GetMinimumScoreFor(mix);
            var gradeMultiplier = scoring.LetterGradeModifierFor(p.Grade, p.Type);
            var plateBonus = mix == MixEnum.Phoenix2 ? scoring.PlateModifierFor(p.Plate, p.Type) : 0;
            var pricedBase = mix == MixEnum.Phoenix2
                ? ScoringConfiguration.Phoenix2PricedBase(p.Type, level)
                : level.BaseRating;
            return new PumbilityWorkedExample(p.Type, p.Level, p.Grade, p.Plate, pricedBase, gradeMultiplier,
                plateBonus, scoring.GetScore(p.Type, level, score, p.Plate));
        }).ToArray();
    }

    /// <summary>
    ///     The constants the page's script multiplies — emitted from the same configuration the
    ///     markup came from, so the script holds no table of its own (D2). Values are exact.
    /// </summary>
    private static string BuildConstantsJson(MixEnum mix, ScoringConfiguration scoring, ChartType type,
        PhoenixLetterGrade anchor, IReadOnlyList<PumbilityLevelRow> rows)
    {
        var payload = new
        {
            mix = mix.ToString(),
            type = type.ToString(),
            // "S24" / "D24" on a mix that prices the types apart; a bare "24" on one that does not.
            prefix = mix == MixEnum.Phoenix2 ? type.GetShortHand() : string.Empty,
            additive = mix == MixEnum.Phoenix2,
            singlesUp = mix == MixEnum.Phoenix2 && type == ChartType.Single,
            anchorGrade = anchor.GetName(),
            levels = rows.OrderBy(r => r.Level).ToDictionary(r => r.Level.ToString(), r => r.PricedBase),
            grades = Grades.ToDictionary(g => g.GetName(), g => scoring.LetterGradeModifierFor(g, type)),
            plates = Plates.ToDictionary(p => p.GetShorthand(),
                p => mix == MixEnum.Phoenix2 ? scoring.PlateModifierFor(p, type) : 1.0),
            floors = Grades.ToDictionary(g => g.GetName(), g => (int)g.GetMinimumScoreFor(mix))
        };
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>Which ramp step (0 … <see cref="RampSteps" />−1) an equivalent level sits at on this type's axis.</summary>
    public static int RampStep(PumbilityCalculatorTypeView type, double equivalentLevel)
    {
        var fraction = (equivalentLevel - type.AxisMin) / (type.AxisMax - type.AxisMin);
        return Math.Clamp((int)Math.Floor(fraction * RampSteps), 0, RampSteps - 1);
    }
}

/// <summary>One chart type's rows, ruler axis, worked examples and script constants.</summary>
public sealed record PumbilityCalculatorTypeView(ChartType Type, IReadOnlyList<PumbilityLevelRow> Levels, int AxisMin,
    int AxisMax, string ConstantsJson);

/// <summary>
///     One level of the value table and one row of the ruler. <see cref="PricedBase" /> is what the
///     formula multiplies for this type — a Phoenix 2 Single's is Base(level + 1). <see cref="Cells" />
///     runs F → SSS+; <see cref="RulerPoints" /> starts two grades below the anchor.
/// </summary>
public sealed record PumbilityLevelRow(int Level, int PricedBase, int ChartCount, IReadOnlyList<PumbilityValueCell> Cells,
    IReadOnlyList<PumbilityRulerPoint> RulerPoints, bool BaseIsExtrapolated)
{
    /// <summary>Levels an SSS+ buys over a 900,000 on this level — the ruler's end label.</summary>
    public double LevelsBought => RulerPoints[^1].EquivalentLevel - Level;
}

/// <summary>Base × grade at the lowest plate, and which level's 900,000 it equals.</summary>
public sealed record PumbilityValueCell(PhoenixLetterGrade Grade, double Value, double EquivalentLevel);

/// <summary>A grade's position on the ruler's axis; <see cref="BelowAnchor" /> draws as the faded tail.</summary>
public sealed record PumbilityRulerPoint(PhoenixLetterGrade Grade, double EquivalentLevel, bool BelowAnchor);

/// <summary>A worked example: the inputs, the arithmetic, the answer.</summary>
public sealed record PumbilityWorkedExample(ChartType Type, int Level, PhoenixLetterGrade Grade, PhoenixPlate Plate,
    int PricedBase, double GradeMultiplier, double PlateBonus, double Value);

/// <summary>The grade ladder as the Doubles table plus the Singles cells that read differently, and the score floors.</summary>
public sealed record PumbilityGradeTable(IReadOnlyDictionary<PhoenixLetterGrade, double> Doubles,
    IReadOnlyDictionary<PhoenixLetterGrade, double> SinglesOverrides, IReadOnlyDictionary<PhoenixLetterGrade, int> Floors);

/// <summary>The plate bonuses as the Doubles table plus the Singles cells that read differently.</summary>
public sealed record PumbilityPlateTable(IReadOnlyDictionary<PhoenixPlate, double> Doubles,
    IReadOnlyDictionary<PhoenixPlate, double> SinglesOverrides);
