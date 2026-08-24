
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Domain;

// Mix-first, no defaults (house port convention): each call scrapes that mix's official
// site and resolves charts against that mix's catalog. UCS and avatar fixes stay mixless.
// Import-path calls take an already-minted session id (SignIn once, reuse it) so one import
// logs in a single time and a background job can carry the session, not the password.
internal interface IOfficialSiteClient
{
    /// <summary>
    ///     Streams the chart boards one at a time as the sweep scrapes them, so the run can
    ///     write and checkpoint per board. Unmapped or failed boards yield a SkipReason
    ///     instead of killing the enumeration.
    /// </summary>
    IAsyncEnumerable<OfficialChartBoardResult> GetOfficialChartBoards(MixEnum mix,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The mix's rating boards. Both mixes publish a PUMBILITY board — Phoenix 2 splits
    ///     it into All/Singles/Doubles tabs whose values keep decimal cents, Phoenix serves
    ///     one whole-number board and additionally keeps its per-level rating lists.
    /// </summary>
    Task<IEnumerable<RatingBoardEntry>> GetRatingBoards(MixEnum mix, CancellationToken cancellationToken);

    Task<string> SignIn(MixEnum mix, string username, string password, CancellationToken cancellationToken);

    Task<int> GetScorePageCount(MixEnum mix, string sid, CancellationToken cancellationToken);

    /// <summary>
    ///     What piugame says the account holds, level by level, plus the live official PUMBILITY —
    ///     around twenty requests. Returns the OFFICIAL side only and never reads our records, so
    ///     the comparison itself stays a pure function (<see cref="CensusDiff" />).
    ///     <para>
    ///         Phoenix's play-data page refuses to break down levels 1–9, so those are recovered as
    ///         a residual against the best-score list's total. Phoenix 2 buckets them properly and
    ///         the extra request is skipped.
    ///     </para>
    /// </summary>
    Task<AccountCensus> GetOfficialCensus(MixEnum mix, Guid userId, string sid,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Every best card at the given <c>?lv=</c> buckets, walked to the end of each — the
    ///     evidence-driven repair for what a census localised. Pass an empty bucket list to walk
    ///     the whole account, which is the deep scan.
    ///     <para>
    ///         The best list is the only surface carrying a SCORE: the play-log modal behind a
    ///         count tile names charts and shows a grade, but nothing that could be saved.
    ///     </para>
    /// </summary>
    Task<IReadOnlyList<OfficialRecordedScore>> GetBestScoresIn(MixEnum mix, Guid userId, string sid,
        IReadOnlyCollection<string> buckets, bool includeBroken, CancellationToken cancellationToken);

    /// <summary>
    ///     <paramref name="maxPages" /> drives the classic (undated) page walk; the dated
    ///     (redesigned) walk ignores it and instead stops on its up-score window. Returns the
    ///     best-list scrape AND the recently-played window it read alongside it, which the
    ///     caller journals as observations.
    /// </summary>
    Task<ScrapedScores> GetRecordedScores(MixEnum mix, Guid userId, string sid, string id,
        bool includeBroken,
        int? maxPages,
        CancellationToken cancellationToken);

    Task<PiuGameAccountDataImport>
        GetAccountData(MixEnum mix, string sid, string? id, CancellationToken cancellationToken);

    Task<IEnumerable<GameCardRecord>> GetGameCards(MixEnum mix, string sid, CancellationToken cancellationToken);

    Task<Contracts.PiuGameAccountIdentity> GetAccountIdentity(MixEnum mix, string username, string password,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ChartPopularityLeaderboardEntry> Entries, IReadOnlyList<MissingChartSighting> Missing)>
        GetOfficialChartLeaderboardEntries(MixEnum mix, CancellationToken cancellationToken);

    Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken);
}
