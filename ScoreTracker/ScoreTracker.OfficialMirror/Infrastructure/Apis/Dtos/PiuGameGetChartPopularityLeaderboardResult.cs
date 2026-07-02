using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetChartPopularityLeaderboardResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        public sealed class Entry
        {
            public Name SongName { get; set; }
            public DifficultyLevel ChartLevel { get; set; }
            public ChartType ChartType { get; set; }
            public int Place { get; set; }
            public string SongImage { get; set; }
        }
    }
}
