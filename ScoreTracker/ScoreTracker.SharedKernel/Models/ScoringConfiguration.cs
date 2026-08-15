using System.ComponentModel;
using System.Data;
using System.Reflection;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models
{
    public sealed class ScoringConfiguration
    {
        private readonly DataTable _dataTable = new();

        public IDictionary<Guid, double>? ChartLevelSnapshot { get; set; }

        public IDictionary<DifficultyLevel, int> LevelRatings { get; set; } =
            DifficultyLevel.All.ToDictionary(l => l, l => l.BaseRating);

        public IDictionary<SongType, double> SongTypeModifiers { get; set; } = Enum
            .GetValues<SongType>()
            .ToDictionary(s => s, s => 1.0);

        public IDictionary<ChartType, double> ChartTypeModifiers { get; set; } = new Dictionary<ChartType, double>
        {
            { ChartType.Single, 1.0 },
            { ChartType.Double, 1.0 },
            { ChartType.CoOp, 1.0 },
            { ChartType.SinglePerformance, 0.0 },
            { ChartType.DoublePerformance, 0.0 }
        };

        public IDictionary<PhoenixLetterGrade, double> LetterGradeModifiers { get; set; } =
            Enum.GetValues<PhoenixLetterGrade>().ToDictionary(l => l, l => l.GetModifier());

        public IDictionary<PhoenixPlate, double> PlateModifiers { get; set; } = Enum.GetValues<PhoenixPlate>()
            .ToDictionary(p => p, p => 1.0);

        /// <summary>
        ///     What a Single prices a grade at when it disagrees with <see cref="LetterGradeModifiers" />.
        ///     Sparse: a grade with no entry here takes the shared value, so only the grades that
        ///     actually differ appear and the two tables cannot drift on the ones that don't.
        ///     <para>
        ///         Null on every configuration that prices both types alike. Serialized
        ///         configurations carry only the shared tables, so one written before a type split
        ///         existed still deserializes and still scores exactly as it did.
        ///     </para>
        /// </summary>
        public IDictionary<PhoenixLetterGrade, double>? SinglesLetterGradeModifiers { get; set; }

        /// <summary>
        ///     What a Single prices a plate at when it disagrees with <see cref="PlateModifiers" />.
        ///     Sparse and optional on the same terms as <see cref="SinglesLetterGradeModifiers" />.
        /// </summary>
        public IDictionary<PhoenixPlate, double>? SinglesPlateModifiers { get; set; }

        public double PgLetterGradeModifier { get; set; } = PhoenixLetterGrade.SSSPlus.GetModifier();

        /// <summary>
        ///     The multiplier a grade earns on a chart of this type — the Singles override when one
        ///     is present for that grade, the shared table otherwise. Every read of the grade table
        ///     inside the formula goes through here, so a type split cannot be honoured on one code
        ///     path and missed on another.
        /// </summary>
        public double LetterGradeModifierFor(PhoenixLetterGrade grade, ChartType chartType)
        {
            return chartType == ChartType.Single && SinglesLetterGradeModifiers != null &&
                   SinglesLetterGradeModifiers.TryGetValue(grade, out var singles)
                ? singles
                : LetterGradeModifiers[grade];
        }

        /// <summary>
        ///     The bonus a plate earns on a chart of this type, resolved exactly as
        ///     <see cref="LetterGradeModifierFor(PhoenixLetterGrade,ChartType)" /> resolves a grade.
        /// </summary>
        public double PlateModifierFor(PhoenixPlate plate, ChartType chartType)
        {
            return chartType == ChartType.Single && SinglesPlateModifiers != null &&
                   SinglesPlateModifiers.TryGetValue(plate, out var singles)
                ? singles
                : PlateModifiers[plate];
        }

        // The mix whose grade cutoffs price score→grade in this config. Phoenix 2 shifted the
        // sub-AAA thresholds, so a P2 config must grade against the P2 table; everything else
        // keeps the original Phoenix table (the default).
        public MixEnum Mix { get; set; } = MixEnum.Phoenix;
        public int MinimumScore { get; set; } = 0;
        public IDictionary<Guid, double> ChartModifiers { get; set; } = new Dictionary<Guid, double>();
        public double StageBreakModifier { get; set; } = 1.0;
        public string CustomAlgorithm { get; set; } = string.Empty;
        public CalculationType Formula { get; set; } = CalculationType.Default;
        public bool AdjustToTime { get; set; } = true;
        public bool ContinuousLetterGradeScale { get; set; } = false;
        private static readonly TimeSpan BaseAverageTime = TimeSpan.FromMinutes(2);

        public double GetScorelessScore(Chart chart, bool includeLevelOverride = true)
        {
            return GetScorelessScore(chart.Id, chart.Level, chart.Type, chart.Song.Type, chart.Song.Duration,
                includeLevelOverride);
        }

        private double GetBaseRating(Guid chartId, ChartType chartType, DifficultyLevel level,
            bool includeLevelOverride)
        {
            double rating = chartType == ChartType.CoOp ? 2000 : LevelRatings[level];
            if (chartType == ChartType.CoOp || ChartLevelSnapshot == null || !includeLevelOverride ||
                !ChartLevelSnapshot.TryGetValue(chartId, out var levelOverride) || level >= 29) return rating;

            var min = DifficultyLevel.From((int)Math.Floor(levelOverride));
            var max = DifficultyLevel.From((int)Math.Ceiling(levelOverride));
            rating = LevelRatings[min] +
                     (LevelRatings[max] - LevelRatings[min]) * (levelOverride - .5 - (int)min);

            return rating;
        }

        private double GetScorelessScore(Guid chartId, DifficultyLevel level, ChartType chartType, SongType songType,
            TimeSpan duration, bool includeLevelOverride)
        {
            var rating = GetBaseRating(chartId, chartType, level, includeLevelOverride);
            var result = rating
                         * ChartTypeModifiers[chartType]
                         * SongTypeModifiers[songType];
            if (ChartModifiers.TryGetValue(chartId, out var cMod)) result *= cMod;
            if (AdjustToTime) result *= duration / BaseAverageTime;

            return result;
        }

        public double GetScore(ChartType type, DifficultyLevel level, PhoenixScore score,
            bool includeLevelOverride = true)
        {
            return GetScore(Guid.Empty, level, type, SongType.Arcade, BaseAverageTime, false, score,
                PhoenixPlate.SuperbGame, includeLevelOverride);
        }

        public double GetScore(DifficultyLevel level, PhoenixScore score, bool includeLevelOverride = true)
        {
            return GetScore(Guid.Empty, level, ChartType.Single, SongType.Arcade, BaseAverageTime, false, score,
                PhoenixPlate.SuperbGame, includeLevelOverride);
        }

        public double GetScore(ChartType type, DifficultyLevel level, PhoenixScore score, PhoenixPlate plate,
            bool isBroken = false, bool includeLevelOverride = true)
        {
            return GetScore(Guid.Empty, level, type, SongType.Arcade, BaseAverageTime, isBroken, score, plate,
                includeLevelOverride);
        }

        private double GetScore(Guid chartId, DifficultyLevel level, ChartType chartType, SongType songType,
            TimeSpan duration, bool isBroken, PhoenixScore score, PhoenixPlate plate, bool includeLevelOverride)
        {
            if (score < MinimumScore) return 0;
            var letterGradeModifier = LetterGradeModifierFor(score, chartType);

            switch (Formula)
            {
                case CalculationType.Default:
                {
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration, includeLevelOverride);
                    result *=
                        letterGradeModifier
                        * PlateModifierFor(plate, chartType);
                    if (ChartModifiers.TryGetValue(chartId, out var chartModifier)) result *= chartModifier;
                    if (isBroken) result *= StageBreakModifier;

                    return result;
                }
                case CalculationType.Avalanche:
                {
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration, includeLevelOverride);
                    result *= PlateModifierFor(plate, chartType);
                    var scoreModifier = letterGradeModifier;
                    if (isBroken) scoreModifier -= StageBreakModifier;
                    return result * scoreModifier;
                }
                case CalculationType.GradePlusPlate:
                {
                    // Phoenix 2 PUMBILITY (per-chart values read off my_page/pumbility.php,
                    // 2026-07-19): charts below level 10 price at ZERO, singles price one level
                    // UP the shared base curve (an S17 is worth Base(18) — the kink at 24 rides
                    // along), and the grade multiplier and plate bonus combine ADDITIVELY
                    // before multiplying the base.
                    if (Mix == MixEnum.Phoenix2 && (int)level < 10) return 0;
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration,
                        includeLevelOverride);
                    if (Mix == MixEnum.Phoenix2 && chartType == ChartType.Single && result > 0)
                        result += (int)level + 1 > 24 ? 10 : 5;
                    result *= letterGradeModifier + PlateModifierFor(plate, chartType);
                    if (isBroken) result *= StageBreakModifier;

                    return result;
                }
                case CalculationType.Custom:
                default:
                {
                    var levelModifier = LevelRatings[level];
                    var chartTypeModifier = ChartTypeModifiers[chartType];
                    var songTypeModifier = SongTypeModifiers[songType];
                    var timeModifier = duration / BaseAverageTime;
                    var scoreModifier = letterGradeModifier;
                    var plateModifier = PlateModifierFor(plate, chartType);
                    var chartModifier = ChartModifiers.TryGetValue(chartId, out var chartModResult)
                        ? chartModResult
                        : 1.0;
                    var brokenModifier = isBroken ? StageBreakModifier : 1.0;
                    var formula = CustomAlgorithm.Replace("LVL", levelModifier.ToString())
                        .Replace("CTYP", chartTypeModifier.ToString())
                        .Replace("STYP", songTypeModifier.ToString())
                        .Replace("TIME", timeModifier.ToString())
                        .Replace("SCOR", score.ToString())
                        .Replace("PLAT", plateModifier.ToString())
                        .Replace("CHRT", chartModifier.ToString())
                        .Replace("LTTR", scoreModifier.ToString())
                        .Replace("BRKN", brokenModifier.ToString());
                    var result = _dataTable.Compute(formula, "");
                    switch (result)
                    {
                        case int intResult:
                            return intResult;
                        case double doubleResult:
                            return doubleResult;
                        case decimal decimalResult:
                            return (double)decimalResult;
                        default:
                            return (double)result;
                    }

                    break;
                }
            }
        }

        public double GetScore(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken,
            bool includeLevelOverride = true)
        {
            return GetScore(chart.Id, chart.Level, chart.Type, chart.Song.Type, chart.Song.Duration, isBroken, score,
                plate, includeLevelOverride);
        }

        /// <summary>
        ///     The grade multiplier a score earns under this configuration, including the
        ///     continuous-scale interpolation and the perfect-game override. Shared with
        ///     <see cref="Decompose(Chart,PhoenixScore,PhoenixPlate,bool,bool)" /> so the split
        ///     cannot answer with a different grade than the total it is splitting.
        /// </summary>
        private double LetterGradeModifierFor(PhoenixScore score, ChartType chartType)
        {
            var letterGrade = score.LetterGradeFor(Mix);
            var letterGradeModifier = LetterGradeModifierFor(letterGrade, chartType);
            if (ContinuousLetterGradeScale && score != 1000000)
            {
                double nextModifier;
                PhoenixScore nextThreshold;
                if (letterGrade != PhoenixLetterGrade.SSSPlus)
                {
                    var nextGrade = letterGrade + 1;
                    nextModifier = LetterGradeModifierFor(nextGrade, chartType);
                    nextThreshold = nextGrade.GetMinimumScoreFor(Mix);
                }
                else
                {
                    nextModifier = PgLetterGradeModifier;
                    nextThreshold = 1000000;
                }

                var threshold = letterGrade.GetMinimumScoreFor(Mix);
                var modifier = LetterGradeModifierFor(letterGrade, chartType);
                letterGradeModifier =
                    modifier + (nextModifier - modifier) * (score - threshold) / (nextThreshold - threshold);
            }
            else if (score == 1000000)
            {
                letterGradeModifier = PgLetterGradeModifier;
            }

            return letterGradeModifier;
        }

        /// <summary>
        ///     What a chart contributes, split into the three things a player can change: which
        ///     chart it is, how well they scored it, and the plate they walked away with. The
        ///     parts sum to <see cref="GetScore(Chart,PhoenixScore,PhoenixPlate,bool,bool)" />
        ///     exactly — it is a decomposition of the formula, not a model of it, and it lives
        ///     beside the formula so the two cannot drift.
        ///     <para>
        ///         Measured from a bare base of ×1.00 rather than from a grade, so the level part
        ///         is the chart's own value and the grade part is everything the score adds on
        ///         top (docs/design/pumbility-overhaul.md D16). On Phoenix 1 that reference is
        ///         also AA, whose modifier is exactly 1.0. On Phoenix 2 one rung sits below it:
        ///         a Single's D is exactly 1.00 and a Single's F is 0.90, so a passing F's grade
        ///         part is slightly NEGATIVE — the score genuinely subtracts from the chart's
        ///         bare value there, and any consumer rendering the parts must tolerate that.
        ///     </para>
        /// </summary>
        public ScoreContribution Decompose(Chart chart, PhoenixScore score, PhoenixPlate plate,
            bool isBroken, bool includeLevelOverride = true)
        {
            if (score < MinimumScore) return default;

            var grade = LetterGradeModifierFor(score, chart.Type);
            var breakModifier = isBroken ? StageBreakModifier : 1.0;
            var plateModifier = PlateModifierFor(plate, chart.Type);

            switch (Formula)
            {
                case CalculationType.Default:
                {
                    // The chart modifier lands twice in this branch — once inside the scoreless
                    // score and once after the grade — so the unit carries both, or the parts
                    // would not sum to the total they are splitting.
                    var unit = GetScorelessScore(chart, includeLevelOverride) * breakModifier;
                    if (ChartModifiers.TryGetValue(chart.Id, out var chartModifier)) unit *= chartModifier;
                    return new ScoreContribution(unit, unit * (grade - 1), unit * grade * (plateModifier - 1));
                }
                case CalculationType.GradePlusPlate:
                {
                    if (Mix == MixEnum.Phoenix2 && (int)chart.Level < 10) return default;
                    var scoreless = GetScorelessScore(chart, includeLevelOverride);
                    if (Mix == MixEnum.Phoenix2 && chart.Type == ChartType.Single && scoreless > 0)
                        scoreless += (int)chart.Level + 1 > 24 ? 10 : 5;
                    var unit = scoreless * breakModifier;
                    return new ScoreContribution(unit, unit * (grade - 1), unit * plateModifier);
                }
                default:
                    // Avalanche and Custom do not separate into these three parts — Avalanche
                    // folds the stage break into the grade term, and Custom is an expression.
                    // Answering anyway would mean inventing a split, so it says so instead.
                    throw new NotSupportedException(
                        $"{Formula} has no level/grade/plate decomposition");
            }
        }

        /// <summary>
        ///     What the best plate available under this configuration would add on top of the one
        ///     held. Asks the formula twice rather than reading the plate table, so it needs to
        ///     know nothing about whether plates multiply (Phoenix) or add (Phoenix 2) — and on
        ///     Phoenix, where every plate modifier is exactly 1.0, it returns zero for that
        ///     reason rather than by a special case.
        /// </summary>
        public double PlateHeadroom(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken = false,
            bool includeLevelOverride = true)
        {
            var best = PlateModifiers.Keys.MaxBy(p => PlateModifierFor(p, chart.Type));
            return Math.Max(0,
                GetScore(chart, score, best, isBroken, includeLevelOverride)
                - GetScore(chart, score, plate, isBroken, includeLevelOverride));
        }

        public enum CalculationType
        {
            [Description("All Modifiers Multiplied")]
            Default,

            [Description("All Modifiers Multiplied * (LetterModifier - BrokenModifier)")]
            Avalanche,
            Custom,

            // Appended after Custom: this enum rides serialized tournament configurations,
            // so existing ordinals must not shift.
            [Description("Base * (LetterModifier + PlateModifier)")]
            GradePlusPlate
        }

        public static double CalculateFungScore(DifficultyLevel level, PhoenixScore score, ChartType? type = null)
        {
            var result = level + (score - 965000.0) / 17500.0;
            if (type == ChartType.Single && level >= 20) result *= Math.Pow(1.008, level - 19);
            return result;
        }

        /// <summary>
        ///     The PUMBILITY formula for a mix — Phoenix and Phoenix 2 compute per-chart
        ///     PUMBILITY differently, so every caller must say which era it is scoring.
        ///     <paramref name="includeCoOp" /> only applies to Phoenix: on Phoenix 2 the
        ///     official formula never counts CO-OP, whatever the caller asks for.
        /// </summary>
        public static ScoringConfiguration PumbilityScoring(MixEnum mix, bool includeCoOp)
        {
            return mix switch
            {
                MixEnum.Phoenix => PhoenixPumbilityScoring(includeCoOp),
                MixEnum.Phoenix2 => Phoenix2PumbilityScoring(),
                _ => throw new ArgumentOutOfRangeException(nameof(mix), mix,
                    "No PUMBILITY formula exists for this mix")
            };
        }

        /// <summary>
        ///     The plate a score most plausibly carries when the score is all you know —
        ///     the modal plate per band across 922,765 real non-broken Phoenix records
        ///     (prod-synced local data, 2026-07-12), crossovers measured at 2k-band
        ///     granularity. SG/EG/RG are never the population mode in any band (real
        ///     plate progression ladders FG → TG → MG → UG), so the expectation never
        ///     emits them. Used by the PUMBILITY projection for unplayed charts;
        ///     deliberately not an exact science — recalibrate per mix once its plate
        ///     data accumulates (same query, new constants;
        ///     docs/design/HomePageWidgets/README.md §5).
        /// </summary>
        public static PhoenixPlate ExpectedPlateForScore(PhoenixScore score)
        {
            return (int)score switch
            {
                >= 1_000_000 => PhoenixPlate.PerfectGame,
                >= 996_000 => PhoenixPlate.UltimateGame,
                >= 972_000 => PhoenixPlate.MarvelousGame,
                >= 964_000 => PhoenixPlate.TalentedGame,
                _ => PhoenixPlate.FairGame
            };
        }

        private static ScoringConfiguration PhoenixPumbilityScoring(bool includeCoOp)
        {
            var config = new ScoringConfiguration
            {
                AdjustToTime = false,
                StageBreakModifier = 0.0
            };
            config.ChartTypeModifiers[ChartType.CoOp] = includeCoOp ? 1 : 0;
            return config;
        }

        /// <summary>
        ///     Phoenix 2's per-level base value: 130 + 5·L, with the growth doubling above 24.
        ///     Verified exact from live data for levels 16–25; the kink at 24 is real.
        /// </summary>
        public static int Phoenix2BaseRating(DifficultyLevel level)
        {
            return 130 + 5 * (int)level + 5 * Math.Max(0, (int)level - 24);
        }

        /// <summary>
        ///     The base a Phoenix 2 chart of this type and level is priced on: Base(level) for a
        ///     Double, one step up the curve for a Single. Callers that project a folder's worth
        ///     have to agree with <see cref="GetScore(Chart,PhoenixScore,PhoenixPlate,bool,bool)" />
        ///     about this, and reaching for <c>Base(level + 1)</c> cannot get there — level 30 is
        ///     off the end of <see cref="DifficultyLevel" />, so clamping under-prices an S29 by
        ///     the ten points it should have gained crossing the kink at 24.
        ///     <para>
        ///         The sub-10 rule is deliberately NOT applied here: this answers what the curve
        ///         charges, and whether a chart contributes at all is the formula's question.
        ///     </para>
        /// </summary>
        public static int Phoenix2PricedBase(ChartType type, DifficultyLevel level)
        {
            var baseRating = Phoenix2BaseRating(level);
            return type == ChartType.Single ? baseRating + ((int)level + 1 > 24 ? 10 : 5) : baseRating;
        }

        /// <summary>
        ///     Phoenix 2's PUMBILITY per-chart formula: contribution =
        ///     Base(level) × (gradeMultiplier + plateBonus), grade and plate combining
        ///     ADDITIVELY — where SINGLES price one level up the base curve (an S17 is worth
        ///     Base(18)) and charts below level 10 price at zero (both verified per-chart from
        ///     my_page/pumbility.php, 2026-07-19). CO-OP, U.C.S. and half-double (performance)
        ///     charts never contribute, and broken plays never contribute. This config prices
        ///     a single chart; the caller aggregates — Singles and Doubles each into their own
        ///     top-50 pool, and the overall total from the top 50 across both types.
        /// </summary>
        private static ScoringConfiguration Phoenix2PumbilityScoring()
        {
            var config = new ScoringConfiguration
            {
                Mix = MixEnum.Phoenix2,
                Formula = CalculationType.GradePlusPlate,
                AdjustToTime = false,
                StageBreakModifier = 0.0,
                // A perfect 1,000,000 stays on the SSS+ grade multiplier — PG's bump is the
                // plate bonus, not a grade override.
                PgLetterGradeModifier = 1.50,
                LevelRatings = DifficultyLevel.All.ToDictionary(l => l, Phoenix2BaseRating)
            };
            config.ChartTypeModifiers[ChartType.CoOp] = 0.0;
            // SinglePerformance/DoublePerformance stay 0 from the defaults (half-double excluded).

            // Grade multipliers. Like the plate table this is what a DOUBLE prices; AA and A+
            // are the two grades a Single reads differently, and they are overridden below.
            // AA+ and up are identical on both types across hundreds of live rows.
            config.LetterGradeModifiers[PhoenixLetterGrade.SSSPlus] = 1.50;
            config.LetterGradeModifiers[PhoenixLetterGrade.SSS] = 1.49;
            config.LetterGradeModifiers[PhoenixLetterGrade.SSPlus] = 1.48;
            config.LetterGradeModifiers[PhoenixLetterGrade.SS] = 1.47;
            config.LetterGradeModifiers[PhoenixLetterGrade.SPlus] = 1.46;
            config.LetterGradeModifiers[PhoenixLetterGrade.S] = 1.45;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus] = 1.43;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAA] = 1.41;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAPlus] = 1.39;
            // A Double reads these two a notch higher than a Single does. Both are live
            // per-chart reads: a D25 A+ FG at 351.52 = Base(25) 260 × (1.35 + 0.002), and a
            // D24 AA RG at 342.50 = Base(24) 250 × 1.37 — the pre-launch value, which turns
            // out to have been a doubles observation rather than stale tuning.
            config.LetterGradeModifiers[PhoenixLetterGrade.AA] = 1.37;
            config.LetterGradeModifiers[PhoenixLetterGrade.APlus] = 1.35;
            // MEASURED. Five import-telemetry rows, at four levels and on three plates, every one
            // of them implying this value and no other: a D24 MG at 326.50 = Base(24) 250 × 1.306,
            // a D25 RG at 338.00 = 260 × 1.300, a D26 FG at 351.54 = 270 × 1.302 and a D27 FG at
            // 364.56 = 280 × 1.302. It was interpolated before those rows arrived and they landed
            // exactly on the guess, which is why the step below is now anchored at both ends.
            config.LetterGradeModifiers[PhoenixLetterGrade.A] = 1.30;
            // MEASURED, and like A it landed on exactly the value that had been interpolated
            // here. A D10 EG B at 227.16 = Base(10) 180 × 1.262, played to close the last open
            // cell in this table. With it the ladder is read rather than fitted end to end, and
            // its real shape is known: −0.05 a rung from A+ all the way down to C, then a single
            // −0.10 at C → D. That one irregular step at the bottom is the whole story of the
            // guesses — extrapolating the uniform step gave the right A and B and the wrong D.
            config.LetterGradeModifiers[PhoenixLetterGrade.B] = 1.25;
            // OBSERVED, one row: a D12 MG C at 229.14 = Base(12) 190 × (1.20 + 0.006). Solving
            // the row the other way round demands a 0.106 plate bonus, which nothing on either
            // plate table comes near, so the grade is what this row measures.
            config.LetterGradeModifiers[PhoenixLetterGrade.C] = 1.20;
            // MEASURED, and it REFUTED the 1.15 this cell used to extrapolate to. A D10 MG D at
            // 199.08 = Base(10) 180 × 1.106, read off the official breakdown page beside a C on
            // the same level and plate (217.08), so the two differ by grade alone: the 18.00
            // between them is 0.10 of base whatever the plate turns out to be worth. The step
            // down from C is therefore DOUBLE the −0.05 the ladder holds higher up, which is what
            // made the uniform-step extrapolation wrong rather than merely unlucky.
            config.LetterGradeModifiers[PhoenixLetterGrade.D] = 1.10;
            // INFERRED, not observed — and F's history demands the label more than any other
            // cell. A passing F IS a rung: the "F is an exclusion, prices at zero" rule
            // (2026-08-12) was refuted on 2026-08-14 by a deliberately played Singles F priced
            // nonzero on the breakdown page, and the observation that had seemed to support the
            // zero turned out to be the sub-10 exclusion wearing an F grade. No Double has ever
            // been priced at F, so this continues the two steps that surround it — D → F is
            // −0.10 on the measured Singles ladder, and the Singles-vs-Doubles gap plateaus at
            // −0.10 across C and D — both of which land on 1.00. A live Doubles F replaces it
            // outright, and CAN now arrive by telemetry: F rows price nonzero, so the observer
            // no longer skips them.
            config.LetterGradeModifiers[PhoenixLetterGrade.F] = 1.00;

            // What a Single reads instead, wherever the two types disagree. Every value here is
            // a live per-chart read off the official breakdown page, and the bracketed count is
            // how many independent rows imply that value and no other. Since the 2026-08-14
            // readings closed the Doubles bottom, every rung of the Singles ladder SSS+ → F is
            // a live read, and on Doubles everything but F is — see the F entry above for the
            // one inference left. AA+ and above land identically on both types, which is why
            // they are absent.
            config.SinglesLetterGradeModifiers = new Dictionary<PhoenixLetterGrade, double>
            {
                [PhoenixLetterGrade.AA] = 1.36, // 42 rows
                [PhoenixLetterGrade.APlus] = 1.33, // 4 rows
                [PhoenixLetterGrade.A] = 1.28, // 4 rows
                [PhoenixLetterGrade.B] = 1.20, // 2 rows
                [PhoenixLetterGrade.C] = 1.10, // 4 rows, at levels 11, 12, 15 and 18
                [PhoenixLetterGrade.D] = 1.00, // 1 row
                // 1 row, played deliberately to settle whether a passing F is a rung at all:
                // Monkey Fingers S12 F MG, official 176.67 = Base(13) 195 × (0.90 + 0.006),
                // exact to the cent. It is — see the shared table's F entry for the reversal.
                [PhoenixLetterGrade.F] = 0.90
            };

            // Plate bonuses (ADDITIVE terms, not multipliers). This table is what a Double
            // prices; Singles differ on two of the eight and say so below.
            config.PlateModifiers[PhoenixPlate.RoughGame] = 0.000;
            config.PlateModifiers[PhoenixPlate.FairGame] = 0.002;
            config.PlateModifiers[PhoenixPlate.TalentedGame] = 0.004;
            config.PlateModifiers[PhoenixPlate.MarvelousGame] = 0.006;
            config.PlateModifiers[PhoenixPlate.SuperbGame] = 0.008;
            config.PlateModifiers[PhoenixPlate.ExtremeGame] = 0.012;
            config.PlateModifiers[PhoenixPlate.UltimateGame] = 0.016;
            config.PlateModifiers[PhoenixPlate.PerfectGame] = 0.020;

            // Singles pay more for the two best-but-imperfect plates. Read off the official
            // per-chart breakdown page during live imports: 21 Extreme Game rows and 60
            // Ultimate Game rows, every one of them implying these values and no other.
            // The remaining six plates land identically on both types, so they are absent
            // here and answer from the table above.
            config.SinglesPlateModifiers = new Dictionary<PhoenixPlate, double>
            {
                [PhoenixPlate.ExtremeGame] = 0.014,
                [PhoenixPlate.UltimateGame] = 0.017
            };
            return config;
        }

        public static ScoringConfiguration PumbilityPlus => CreateScoring();

        private static ScoringConfiguration CreateScoring()
        {
            var result = new ScoringConfiguration
            {
                ContinuousLetterGradeScale = true
            };
            result.LetterGradeModifiers[PhoenixLetterGrade.AAA] = 1.0;
            result.LetterGradeModifiers[PhoenixLetterGrade.AAPlus] = .9;
            result.LetterGradeModifiers[PhoenixLetterGrade.AA] = .75;
            result.LetterGradeModifiers[PhoenixLetterGrade.APlus] = .50;
            result.LetterGradeModifiers[PhoenixLetterGrade.A] = 0;
            result.LetterGradeModifiers[PhoenixLetterGrade.B] = 0;
            result.LetterGradeModifiers[PhoenixLetterGrade.C] = 0;
            result.LetterGradeModifiers[PhoenixLetterGrade.D] = 0;
            result.LetterGradeModifiers[PhoenixLetterGrade.F] = 0;
            result.ChartTypeModifiers[ChartType.CoOp] = 1.0;
            result.AdjustToTime = false;
            result.PgLetterGradeModifier = 1.6;
            result.LevelRatings[1] = 10;
            result.LevelRatings[2] = 20;
            result.LevelRatings[3] = 30;
            result.LevelRatings[4] = 40;
            result.LevelRatings[5] = 50;
            result.LevelRatings[6] = 60;
            result.LevelRatings[7] = 70;
            result.LevelRatings[8] = 80;
            result.LevelRatings[9] = 90;
            return result;
        }
    }
}

public static class ScoringConfigHelpers
{
    public static string GetDescription(this ScoringConfiguration.CalculationType enumValue)
    {
        return typeof(ScoringConfiguration.CalculationType).GetField(enumValue.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? string.Empty;
    }
}