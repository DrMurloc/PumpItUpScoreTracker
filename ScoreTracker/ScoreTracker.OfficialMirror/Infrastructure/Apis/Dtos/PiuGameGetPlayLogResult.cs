using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    /// <summary>
    ///     The charts behind one play-data count tile — the modal the site opens when a tile is
    ///     clicked. Six rows a page, half of what <c>my_best_score.php</c> serves, so this is the
    ///     cheaper enumeration only when a histogram has already narrowed the gap to a small cell.
    ///     Rows carry chart identity but no score; the repair reads scores from the best list.
    /// </summary>
    internal sealed class PiuGameGetPlayLogResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        /// <summary>Highest page the pager offers. 1 when the cell fits on one page.</summary>
        public int MaxPage { get; set; } = 1;

        internal sealed class Entry
        {
            public string SongName { get; set; } = string.Empty;
            public ChartType ChartType { get; set; }
            public int Level { get; set; }
        }
    }
}
