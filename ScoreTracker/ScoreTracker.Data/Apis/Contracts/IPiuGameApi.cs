using ScoreTracker.Data.Apis.Dtos;

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

        Task<HttpClient> GetSessionId(string username, string password, CancellationToken cancellationToken);

        Task<PiuGameGetBestScoresResult>
            GetBestScores(HttpClient client, int page, CancellationToken cancellationToken);
    }
}
