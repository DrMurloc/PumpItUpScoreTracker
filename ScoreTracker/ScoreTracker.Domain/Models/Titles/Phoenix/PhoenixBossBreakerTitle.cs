using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix
{
    public sealed class PhoenixBossBreakerTitle : PhoenixTitle
    {
        private readonly Name _songName;
        private readonly ChartType _chartType;
        private readonly DifficultyLevel _level;

        public PhoenixBossBreakerTitle(Name mix, Name songName, ChartType chartType, DifficultyLevel level,
            bool includeChartType = true) : base(
            $"[{mix}] {(includeChartType ? chartType + " " : "")}Boss breaker",
            $"Pass {songName} {chartType.GetShortHand()}{level}", "Boss Breaker",
            1)
        {
            _songName = songName;
            _chartType = chartType;
            _level = level;
        }

        public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
        {
            if (chart.Song.Name == _songName && _chartType == chart.Type && _level == chart.Level &&
                !attempt.IsBroken) return 1;

            return 0;
        }
    }
}
