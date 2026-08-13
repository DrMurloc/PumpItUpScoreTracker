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

        /// <summary>
        ///     The badge index beside the total — the page's one statement of the player's
        ///     PUMBILITY level, 0 (unranked) through 36 (docs/design/pumbility-levels.md). Null
        ///     when the page draws none: Phoenix, or a redesign — the parser never guesses.
        /// </summary>
        public int? BadgeIndex { get; set; }

        /// <summary>
        ///     The badge img's own URL, absolute — the mirror's copy source. Carried raw because
        ///     the source's zero-padding flips at ten, and rebuilding the name is how the bottom
        ///     of the ladder once looked unpublished. Null exactly when <see cref="BadgeIndex" /> is.
        /// </summary>
        public Uri? BadgeImageUrl { get; set; }

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
