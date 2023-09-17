using System.Data;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models
{
    public sealed class StaminaSession
    {
        private readonly StaminaSessionConfiguration _configuration;
        public ICollection<Entry> Entries { get; }

        public int CurrentScore { get; }

        public StaminaSession(StaminaSessionConfiguration configuration)
        {
            _configuration = configuration;
            Entries = new List<Entry>();
            CurrentScore = 0;
        }

        public TimeSpan CurrentRestTime => _configuration.MaxTime - TotalPlayTime;

        public TimeSpan AverageTimeBetweenCharts =>
            Entries.Count <= 1 ? _configuration.MaxTime : CurrentRestTime / Entries.Count;

        public TimeSpan AverageTimeWithAddedChart(Chart chart)
        {
            var charts = Entries.Select(e => e.Chart).Append(chart).ToArray();
            var totalPlayTime = TimeSpan.FromTicks(charts.Sum(c => c.Song.Duration.Ticks));
            var restTime = _configuration.MaxTime - totalPlayTime;

            return charts.Length <= 1 ? _configuration.MaxTime : restTime / charts.Length;
        }

        public int TotalScore => Entries.Sum(c => c.SessionScore);

        public TimeSpan TotalPlayTime =>
            TimeSpan.FromTicks(Entries.Select(e => e.Chart).Sum(c => c.Song.Duration.Ticks));

        public bool CanAdd(Chart chart)
        {
            if (_configuration.GetScorelessScore(chart) == 0) return false;
            if (TotalPlayTime + chart.Song.Duration > _configuration.MaxTime)
            {
                return false;
            }

            return _configuration.AllowRepeats || !Entries.Any(c =>
                c.Chart.Level == chart.Level && c.Chart.Type == chart.Type && c.Chart.Song.Name == chart.Song.Name);
        }

        public void Add(Chart chart, PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            if (!CanAdd(chart))
            {
                throw new ArgumentException($"{chart.Song.Name} {chart.DifficultyString} is invalid for this session");
            }

            Entries.Add(
                new Entry(chart, score, plate, isBroken, _configuration.GetScore(chart, score, plate, isBroken)));
        }

        public sealed record Entry(Chart Chart, PhoenixScore Score, PhoenixPlate Plate, bool IsBroken, int SessionScore)
        {
        }
    }
}