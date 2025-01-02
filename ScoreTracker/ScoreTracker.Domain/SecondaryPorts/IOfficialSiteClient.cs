
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IOfficialSiteClient
{
    Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(
        CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(CancellationToken cancellationToken);
    Task<int> GetScorePageCount(string username, string password, CancellationToken cancellationToken);

    Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(Guid userId, string username, string password, string id,
        bool includeBroken,
        int? maxPages,
        CancellationToken cancellationToken);


    Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> GetRecentScores(string username,
        string password,
        CancellationToken cancellationToken);

    Task<PiuGameAccountDataImport>
        GetAccountData(string username, string password, CancellationToken cancellationToken);

    Task<IEnumerable<GameCardRecord>> GetGameCards(string username, string password,
        CancellationToken cancellationToken);

    Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(
        CancellationToken cancellationToken);

    Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken);

    Task FixAvatars();
}