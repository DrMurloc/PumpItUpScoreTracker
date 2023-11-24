
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IOfficialSiteClient
{
    Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(
        CancellationToken cancellationToken);

    Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(CancellationToken cancellationToken);
}