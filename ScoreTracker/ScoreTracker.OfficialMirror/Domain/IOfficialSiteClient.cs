
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Domain;

// Mix-first, no defaults (house port convention): each call scrapes that mix's official
// site and resolves charts against that mix's catalog. UCS and avatar fixes stay mixless.
// Import-path calls take an already-minted session id (SignIn once, reuse it) so one import
// logs in a single time and a background job can carry the session, not the password.
internal interface IOfficialSiteClient
{
    Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(MixEnum mix,
        CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken);

    Task<string> SignIn(MixEnum mix, string username, string password, CancellationToken cancellationToken);

    Task<int> GetScorePageCount(MixEnum mix, string sid, CancellationToken cancellationToken);

    Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(MixEnum mix, Guid userId, string sid, string id,
        bool includeBroken,
        int? maxPages,
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

    Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken);

    Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken);

    Task FixAvatars();
}
