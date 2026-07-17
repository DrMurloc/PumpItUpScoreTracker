using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetChartPopularityLeaderboardResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        /// <summary>
        ///     Tiles the endpoint served, parseable or not. Pagination decisions belong on
        ///     this — a full page of 50 with a few unparseable tiles must not end a walk.
        /// </summary>
        public int RawRowCount { get; set; }

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
