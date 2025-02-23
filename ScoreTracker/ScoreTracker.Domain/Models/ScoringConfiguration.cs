using System.ComponentModel;
using System.Data;
using System.Reflection;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
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
                !ChartLevelSnapshot.TryGetValue(chartId, out var levelOverride) || level >= 28) return rating;

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
            Custom
        }

        public static double CalculateFungScore(DifficultyLevel level, PhoenixScore score, ChartType? type = null)
        {
            var result = level + (score - 965000.0) / 17500.0;
            if (type == ChartType.Single && level >= 20) result *= Math.Pow(1.008, level - 19);
            return result;
        }

        public static ScoringConfiguration PumbilityScoring(bool includeCoOp)
        {
            var config = new ScoringConfiguration
            {
                AdjustToTime = false,
                StageBreakModifier = 0.0
            };
            config.ChartTypeModifiers[ChartType.CoOp] = includeCoOp ? 1 : 0;
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