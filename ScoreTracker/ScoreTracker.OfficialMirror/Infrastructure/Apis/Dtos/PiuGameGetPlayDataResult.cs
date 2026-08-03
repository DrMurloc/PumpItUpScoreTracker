namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    /// <summary>
    ///     One <c>my_page/play_data.php</c> bucket. The page counts PASSES only — a stage break
    ///     appears nowhere on it — which is what makes it comparable against our non-broken
    ///     records without knowing whether the player imports breaks.
    ///     <para>
    ///         The two mixes render it differently and the ACL hides that: Phoenix 2's tiles are
    ///         CUMULATIVE ("this grade or better") while Phoenix's are exact, so
    ///         <see cref="GradeCounts" /> and <see cref="PlateCounts" /> are always de-cumulated to
    ///         exact per-band counts. Phoenix has no grade tiles at all, and its level filter
    ///         starts at 10; Phoenix 2's reaches down to 1.
    ///     </para>
    /// </summary>
    internal sealed class PiuGameGetPlayDataResult
    {
        /// <summary>The <c>?lv=</c> value this page was filtered to — "" (All), "18", "27over", "coop".</summary>
        public string Bucket { get; set; } = string.Empty;

        /// <summary>Charts passed in this bucket.</summary>
        public int Passes { get; set; }

        /// <summary>The mix's chart count for the bucket — the "/ 3,646" half. Null when unrendered.</summary>
        public int? CatalogTotal { get; set; }

        /// <summary>Exact counts per grade token (SSS_PLUS … F). Empty on Phoenix, which has no grade tiles.</summary>
        public IReadOnlyDictionary<string, int> GradeCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>Exact counts per plate token (pg … rg).</summary>
        public IReadOnlyDictionary<string, int> PlateCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>Every <c>?lv=</c> option the page offers, read off the page rather than assumed.</summary>
        public string[] Buckets { get; set; } = Array.Empty<string>();
    }
}
