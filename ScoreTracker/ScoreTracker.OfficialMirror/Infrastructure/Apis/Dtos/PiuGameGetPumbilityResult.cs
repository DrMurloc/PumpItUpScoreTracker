using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    /// <summary>
    ///     <c>my_page/pumbility.php</c> — the official pool, per chart, with the site's own value
    ///     for each. Both mixes publish it and both are LIVE, unlike the ranking board's daily
    ///     01:00 KST batch, so this is the only trustworthy "official PUMBILITY right now".
    ///     <para>
    ///         The grammars differ (Phoenix 2 renders <c>li &gt; div.top-wrap</c> cards, Phoenix the
    ///         classic ranking list) and the parser picks by page shape, never by mix. Phoenix
    ///         PUMBILITY is plate-blind, so its rows carry no plate.
    ///     </para>
    /// </summary>
    internal sealed class PiuGameGetPumbilityResult
    {
        public double Total { get; set; }
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        internal sealed class Entry
        {
            public string SongName { get; set; } = string.Empty;
            public ChartType ChartType { get; set; }
            public int Level { get; set; }

            /// <summary>The site's own contribution for this chart. Zero is meaningful — that is how
            /// the page prices sub-level-10 and broken entries.</summary>
            public double Value { get; set; }

            public PhoenixLetterGrade? Grade { get; set; }
            public PhoenixPlate? Plate { get; set; }
        }
    }
}
