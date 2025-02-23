using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class TournamentConfigurationJsonEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public IDictionary<int, int> LevelRatings { get; set; } = new Dictionary<int, int>();
        public IDictionary<string, double> SongTypeModifiers { get; set; } = new Dictionary<string, double>();

        public IDictionary<string, double> ChartTypeModifiers { get; set; } = new Dictionary<string, double>();

        public IDictionary<string, double> LetterGradeModifiers { get; set; } = new Dictionary<string, double>();

        public IDictionary<string, double> PlateModifiers { get; set; } = new Dictionary<string, double>();
        public IDictionary<Guid, double> ChartModifiers { get; set; } = new Dictionary<Guid, double>();
        public int CalculationType { get; set; } = (int)ScoringConfiguration.CalculationType.Default;
        public double? PgModifier { get; set; }
        public int MinimumScore { get; set; }
        public string CustomFormula { get; set; } = string.Empty;

        public double StageBreakModifier { get; set; }
        public bool AdjustToTime { get; set; }
        public bool ContinuousLetterGradeScale { get; set; } = false;
        public TimeSpan MaxTime { get; set; } = TimeSpan.Zero;
        public bool AllowRepeats { get; set; }

        public static TournamentConfigurationJsonEntity From(TournamentConfiguration config)
        {
            return new TournamentConfigurationJsonEntity
            {
                Id = config.Id,
                Name = config.Name,
                StartDate = config.StartDate,
                EndDate = config.EndDate,
                PgModifier = config.Scoring.PgLetterGradeModifier,
                MinimumScore = config.Scoring.MinimumScore,
                CustomFormula = config.Scoring.CustomAlgorithm,
                CalculationType = (int)config.Scoring.Formula,
                ChartModifiers = config.Scoring.ChartModifiers,
                ContinuousLetterGradeScale = config.Scoring.ContinuousLetterGradeScale,
                StageBreakModifier = config.Scoring.StageBreakModifier,
                AdjustToTime = config.Scoring.AdjustToTime,
                MaxTime = config.MaxTime,
                AllowRepeats = config.AllowRepeats,
                LevelRatings = config.Scoring.LevelRatings.ToDictionary(kv => (int)kv.Key, kv => kv.Value),
                SongTypeModifiers =
                    config.Scoring.SongTypeModifiers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                ChartTypeModifiers =
                    config.Scoring.ChartTypeModifiers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                LetterGradeModifiers =
                    config.Scoring.LetterGradeModifiers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                PlateModifiers = config.Scoring.PlateModifiers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
            };
        }

        public TournamentConfiguration To(IDictionary<Guid, double>? chartLevelSnapshot)
        {
            return new TournamentConfiguration(Id, Name, new ScoringConfiguration
            {
                StageBreakModifier = StageBreakModifier,
                AdjustToTime = AdjustToTime,
                CustomAlgorithm = CustomFormula,
                MinimumScore = MinimumScore,
                ChartLevelSnapshot = chartLevelSnapshot,
                ChartModifiers = ChartModifiers,
                Formula = (ScoringConfiguration.CalculationType)CalculationType,
                PgLetterGradeModifier = PgModifier ?? LetterGradeModifiers[PhoenixLetterGrade.SSSPlus.ToString()],
                ContinuousLetterGradeScale = ContinuousLetterGradeScale,
                LevelRatings = LevelRatings.ToDictionary(kv => (DifficultyLevel)kv.Key, kv => kv.Value),
                SongTypeModifiers = SongTypeModifiers.ToDictionary(kv => Enum.Parse<SongType>(kv.Key), kv => kv.Value),
                ChartTypeModifiers =
                    ChartTypeModifiers.ToDictionary(kv => Enum.Parse<ChartType>(kv.Key), kv => kv.Value),
                LetterGradeModifiers =
                    LetterGradeModifiers.ToDictionary(kv => Enum.Parse<PhoenixLetterGrade>(kv.Key), kv => kv.Value),
                PlateModifiers = PlateModifiers.ToDictionary(kv => Enum.Parse<PhoenixPlate>(kv.Key), kv => kv.Value)
            }, false, false)
            {
                StartDate = StartDate,
                EndDate = EndDate,
                MaxTime = MaxTime,
                AllowRepeats = AllowRepeats
            };
        }
    }
}