using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class StaminaSession
    {
        private readonly StaminaSessionConfiguration _configuration;
        public ICollection<Chart> Charts { get; }
        public IDictionary<Guid, PhoenixScore> Scores { get; } = new Dictionary<Guid, PhoenixScore>();
        public IDictionary<Guid, PhoenixPlate> Plates { get; } = new Dictionary<Guid, PhoenixPlate>();
        public IDictionary<Guid, int> SessionScores { get; } = new Dictionary<Guid, int>();
        public int CurrentScore { get; }

        public StaminaSession(StaminaSessionConfiguration configuration)
        {
            _configuration = configuration;
            Charts = new List<Chart>();
            CurrentScore = 0;
        }

        public TimeSpan CurrentRestTime => _configuration.MaxTime - TotalPlayTime;

        public TimeSpan AverageTimeBetweenCharts =>
            Charts.Count <= 1 ? _configuration.MaxTime : CurrentRestTime / Charts.Count;

        public TimeSpan AverageTimeWithAddedChart(Chart chart)
        {
            var charts = Charts.Append(chart).ToArray();
            var totalPlayTime = TimeSpan.FromTicks(charts.Sum(c => c.Song.Duration.Ticks));
            var restTime = _configuration.MaxTime - totalPlayTime;

            return charts.Length <= 1 ? _configuration.MaxTime : restTime / charts.Length;
        }

        public int TotalScore => SessionScores.Values.Sum();

        public TimeSpan TotalPlayTime => TimeSpan.FromTicks(Charts.Sum(c => c.Song.Duration.Ticks));

        public bool CanAdd(Chart chart)
        {
            if (_configuration.GetScorelessScore(chart) == 0) return false;
            if (TotalPlayTime + chart.Song.Duration > _configuration.MaxTime)
            {
                return false;
            }

            return _configuration.AllowRepeats || !Charts.Any(c =>
                c.Level == chart.Level && c.Type == chart.Type && c.Song.Name == chart.Song.Name);
        }

        public void Add(Chart chart, PhoenixScore score, PhoenixPlate plate)
        {
            if (!CanAdd(chart))
            {
                throw new ArgumentException($"{chart.Song.Name} {chart.DifficultyString} is invalid for this session");
            }

            Scores[chart.Id] = score;
            Plates[chart.Id] = plate;
            Charts.Add(chart);
            SessionScores[chart.Id] = _configuration.GetScore(chart, score, plate);
        }
    }
}