using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetChartPopularityLeaderboardResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        public sealed class Entry
        {
            public Name SongName { get; set; }
            public DifficultyLevel ChartLevel { get; set; }
            public ChartType ChartType { get; set; }
            public int Place { get; set; }
        }
    }
}
