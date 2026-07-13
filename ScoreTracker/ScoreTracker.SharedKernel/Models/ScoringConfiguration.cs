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

        public double PgLetterGradeModifier { get; set; } = PhoenixLetterGrade.SSSPlus.GetModifier();
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
            var letterGrade = score.LetterGrade;
            var letterGradeModifier = LetterGradeModifiers[letterGrade];
            if (ContinuousLetterGradeScale && score != 1000000)
            {
                double nextModifier;
                PhoenixScore nextThreshold;
                if (letterGrade != PhoenixLetterGrade.SSSPlus)
                {
                    var nextGrade = letterGrade + 1;
                    nextModifier = LetterGradeModifiers[nextGrade];
                    nextThreshold = nextGrade.GetMinimumScore();
                }
                else
                {
                    nextModifier = PgLetterGradeModifier;
                    nextThreshold = 1000000;
                }

                var threshold = letterGrade.GetMinimumScore();
                var modifier = LetterGradeModifiers[letterGrade];
                letterGradeModifier =
                    modifier + (nextModifier - modifier) * (score - threshold) / (nextThreshold - threshold);
            }
            else if (score == 1000000)
            {
                letterGradeModifier = PgLetterGradeModifier;
            }

            switch (Formula)
            {
                case CalculationType.Default:
                {
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration, includeLevelOverride);
                    result *=
                        letterGradeModifier
                        * PlateModifiers[plate];
                    if (ChartModifiers.TryGetValue(chartId, out var chartModifier)) result *= chartModifier;
                    if (isBroken) result *= StageBreakModifier;

                    return result;
                }
                case CalculationType.Avalanche:
                {
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration, includeLevelOverride);
                    result *= PlateModifiers[plate];
                    var scoreModifier = letterGradeModifier;
                    if (isBroken) scoreModifier -= StageBreakModifier;
                    return result * scoreModifier;
                }
                case CalculationType.GradePlusPlate:
                {
                    // Phoenix 2 PUMBILITY: the grade multiplier and the plate bonus combine
                    // ADDITIVELY before multiplying the base (validated against real per-chart
                    // pumbility samples; a multiplicative plate overshoots every sample).
                    var result = GetScorelessScore(chartId, level, chartType, songType, duration,
                        includeLevelOverride);
                    result *= letterGradeModifier + PlateModifiers[plate];
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
                    var plateModifier = PlateModifiers[plate];
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
        ///     docs/design/home-page-widgets.md §5).
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
        ///     Phoenix 2's official PUMBILITY per-chart formula, reverse-engineered from the
        ///     live pumbility rankings + per-chart leaderboards and validated against owner-
        ///     collected real per-chart values (2026-07): contribution =
        ///     Base(level) × (gradeMultiplier + plateBonus) — grade and plate combine
        ///     ADDITIVELY. CO-OP, U.C.S. and half-double (performance) charts never
        ///     contribute (official site text), and broken plays never contribute
        ///     (owner-confirmed). This config only prices a single chart; the caller
        ///     aggregates. The per-type Singles and Doubles totals are each their own
        ///     top-50 pool (the site's ?t=s / ?t=d boards), but overall PUMBILITY is ONE
        ///     merged top-50 across both types — confirmed from the live "All" board
        ///     (2026-07-13), NOT the two per-type pools summed.
        /// </summary>
        private static ScoringConfiguration Phoenix2PumbilityScoring()
        {
            var config = new ScoringConfiguration
            {
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

            // Grade multipliers — verified exact for A+ and above.
            config.LetterGradeModifiers[PhoenixLetterGrade.SSSPlus] = 1.50;
            config.LetterGradeModifiers[PhoenixLetterGrade.SSS] = 1.49;
            config.LetterGradeModifiers[PhoenixLetterGrade.SSPlus] = 1.48;
            config.LetterGradeModifiers[PhoenixLetterGrade.SS] = 1.47;
            config.LetterGradeModifiers[PhoenixLetterGrade.SPlus] = 1.46;
            config.LetterGradeModifiers[PhoenixLetterGrade.S] = 1.45;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus] = 1.43;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAA] = 1.41;
            config.LetterGradeModifiers[PhoenixLetterGrade.AAPlus] = 1.39;
            config.LetterGradeModifiers[PhoenixLetterGrade.AA] = 1.37;
            config.LetterGradeModifiers[PhoenixLetterGrade.APlus] = 1.35;
            // TODO(P2-pumbility): grades below A+ are UNVERIFIED — no live sample exists yet.
            // Pattern-extended at −0.02 per step pending real data.
            config.LetterGradeModifiers[PhoenixLetterGrade.A] = 1.33;
            config.LetterGradeModifiers[PhoenixLetterGrade.B] = 1.31;
            config.LetterGradeModifiers[PhoenixLetterGrade.C] = 1.29;
            config.LetterGradeModifiers[PhoenixLetterGrade.D] = 1.27;
            config.LetterGradeModifiers[PhoenixLetterGrade.F] = 1.25;

            // Plate bonuses (ADDITIVE terms, not multipliers).
            // TODO(P2-pumbility): community data suggested singles-specific UG/EG/RG values
            // (.017/.014/−.010); treated as a data error for now (owner call 2026-07-09) —
            // the doubles-verified table applies to both types. Adjust here if live singles
            // data disagrees, then run the P2 recalculation job.
            config.PlateModifiers[PhoenixPlate.RoughGame] = 0.000;
            config.PlateModifiers[PhoenixPlate.FairGame] = 0.002;
            config.PlateModifiers[PhoenixPlate.TalentedGame] = 0.004;
            config.PlateModifiers[PhoenixPlate.MarvelousGame] = 0.006;
            config.PlateModifiers[PhoenixPlate.SuperbGame] = 0.008;
            config.PlateModifiers[PhoenixPlate.ExtremeGame] = 0.012;
            config.PlateModifiers[PhoenixPlate.UltimateGame] = 0.016;
            config.PlateModifiers[PhoenixPlate.PerfectGame] = 0.020;
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