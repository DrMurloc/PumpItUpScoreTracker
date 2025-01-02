using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Data.Apis.Contracts
{
    public interface IPiuGameApi
    {
        Task<PiuGameGetSongsResult> Get20AboveSongs(int page, CancellationToken cancellationToken);
        Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(string songId, CancellationToken cancellationToken);
        Task<PiuGameGetLeaderboardListResult> GetLeaderboards(CancellationToken cancellationToken);
        Task<PiuGameGetLeaderboardResult> GetLeaderboard(string leaderboardId, CancellationToken cancellationToken);

        Task<PiuGameGetChartPopularityLeaderboardResult> GetChartPopularityLeaderboard(int page,
            CancellationToken cancellationToken);

        Task<IEnumerable<PiuGameGetRecentScoresResult>> GetRecentScores(HttpClient client,
            CancellationToken cancellationToken);

        Task<HttpClient> GetSessionId(string username, string password, CancellationToken cancellationToken);

        Task<PiuGameGetBestScoresResult>
            GetBestScores(HttpClient client, int page, CancellationToken cancellationToken);

        Task<PiuGameGetAccountDataResult> GetAccountData(HttpClient client,
            CancellationToken cancellationToken);

        Task<PiuGameGetUcsResult?> GetUcs(int ucsId, CancellationToken cancellationToken);
        Task<IEnumerable<GameCardRecord>> GetCards(HttpClient client, CancellationToken cancellationToken);
        Task SetCard(HttpClient client, string id, CancellationToken cancellationToken);
    }
}
