﻿
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IOfficialSiteClient
{
    Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(
        CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(CancellationToken cancellationToken);
    Task<int> GetScorePageCount(string username, string password, CancellationToken cancellationToken);

    Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(string username, string password, int? maxPages,
        CancellationToken cancellationToken);

    Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(
        CancellationToken cancellationToken);
}