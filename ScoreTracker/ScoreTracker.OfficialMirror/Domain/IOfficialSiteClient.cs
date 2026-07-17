
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
    ///     The mix's rating boards: Phoenix's per-level rating lists, Phoenix 2's PUMBILITY
    ///     All/Singles/Doubles tabs (values keep their decimal cents).
    /// </summary>
    Task<IEnumerable<RatingBoardEntry>> GetRatingBoards(MixEnum mix, CancellationToken cancellationToken);

    Task<string> SignIn(MixEnum mix, string username, string password, CancellationToken cancellationToken);

    Task<int> GetScorePageCount(MixEnum mix, string sid, CancellationToken cancellationToken);

    /// <summary>
    ///     <paramref name="maxPages" /> drives the classic (undated) page walk;
    ///     <paramref name="since" /> is the saved-date watermark that cuts the dated walk
    ///     short. Each strategy ignores the other's parameter.
    /// </summary>
    Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(MixEnum mix, Guid userId, string sid, string id,
        bool includeBroken,
        int? maxPages,
        DateTimeOffset? since,
        CancellationToken cancellationToken);


    Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> GetRecentScores(MixEnum mix,
        string username,
        string password,
        CancellationToken cancellationToken);

    Task<PiuGameAccountDataImport>
        GetAccountData(MixEnum mix, string sid, string? id, CancellationToken cancellationToken);

    Task<IEnumerable<GameCardRecord>> GetGameCards(MixEnum mix, string sid, CancellationToken cancellationToken);

    Task<Contracts.PiuGameAccountIdentity> GetAccountIdentity(MixEnum mix, string username, string password,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ChartPopularityLeaderboardEntry> Entries, IReadOnlyList<MissingChartSighting> Missing)>
        GetOfficialChartLeaderboardEntries(MixEnum mix, CancellationToken cancellationToken);

    Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken);

    Task FixAvatars();
}
