
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Domain;

// Mix-first, no defaults (house port convention): each call scrapes that mix's official
// site and resolves charts against that mix's catalog. UCS and avatar fixes stay mixless.
internal interface IOfficialSiteClient
{
    Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(MixEnum mix,
        CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken);

    Task<int> GetScorePageCount(MixEnum mix, string username, string password,
        CancellationToken cancellationToken);

    Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(MixEnum mix, Guid userId, string username,
        string password, string id,
        bool includeBroken,
        int? maxPages,
        CancellationToken cancellationToken);


    Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> GetRecentScores(MixEnum mix,
        string username,
        string password,
        CancellationToken cancellationToken);

    Task<PiuGameAccountDataImport>
        GetAccountData(MixEnum mix, string username, string password, string? id,
            CancellationToken cancellationToken);

    Task<IEnumerable<GameCardRecord>> GetGameCards(MixEnum mix, string username, string password,
        CancellationToken cancellationToken);

    Task<Contracts.PiuGameAccountIdentity> GetAccountIdentity(MixEnum mix, string username, string password,
        CancellationToken cancellationToken);

    Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken);

    Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken);

    Task FixAvatars();
}
