using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Domain.Models
{
    public sealed class ScoringConfiguration
    {
        public IDictionary<DifficultyLevel, int> LevelRatings { get; set; } =
            DifficultyLevel.All.ToDictionary(l => l, l => l.BaseRating);

        public IDictionary<SongType, double> SongTypeModifiers { get; set; } = Enum
            .GetValues<SongType>()
            .ToDictionary(s => s, s => 1.0);

        public IDictionary<ChartType, double> ChartTypeModifiers { get; set; } = new Dictionary<ChartType, double>()
        {
            { ChartType.Single, 1.0 },
            { ChartType.Double, 1.0 },
            { ChartType.CoOp, 0.0 },
            { ChartType.SinglePerformance, 0.0 },
            { ChartType.DoublePerformance, 0.0 }
        };

        public IDictionary<PhoenixLetterGrade, double> LetterGradeModifiers { get; set; } =
            Enum.GetValues<PhoenixLetterGrade>().ToDictionary(l => l, l => l.GetModifier());

        public IDictionary<PhoenixPlate, double> PlateModifiers { get; set; } = Enum.GetValues<PhoenixPlate>()
            .ToDictionary(p => p, p => 1.0);

        public double StageBreakModifier { get; set; } = 1.0;
        public bool AdjustToTime { get; set; } = true;
        public bool ContinuousLetterGradeScale { get; set; } = false;
        private static readonly TimeSpan BaseAverageTime = TimeSpan.FromMinutes(2);

        public double GetScorelessScore(Chart chart)
        {
            var result = (double)LevelRatings[chart.Level]
                         * ChartTypeModifiers[chart.Type]
                         * SongTypeModifiers[chart.Song.Type];
            if (AdjustToTime) result *= chart.Song.Duration / BaseAverageTime;

            return result;
        }


        public int GetScore(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            var result = GetScorelessScore(chart);
            var letterGrade = score.LetterGrade;
            var letterGradeModifier = LetterGradeModifiers[letterGrade];
            if (ContinuousLetterGradeScale && letterGrade != PhoenixLetterGrade.SSSPlus)
            {
                var nextGrade = letterGrade + 1;
                var threshold = letterGrade.GetMinimumScore();
                var nextThreshold = nextGrade.GetMinimumScore();
                var modifier = LetterGradeModifiers[letterGrade];
                var nextModifier = LetterGradeModifiers[nextGrade];
                letterGradeModifier =
                    modifier + (nextModifier - modifier) * (score - threshold) / (nextThreshold - threshold);
            }

            result *=
                letterGradeModifier
                * PlateModifiers[plate];
            if (isBroken) result *= StageBreakModifier;

            return (int)result;
        }
    }
}
