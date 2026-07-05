using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts
{
    // Every URL-building call takes the mix first: Phoenix and Phoenix 2 are structurally
    // identical sites on different hosts (PiuGameConfiguration.BaseUrlFor). GetUcs stays
    // mixless — UCS has a single shared site.
    internal interface IPiuGameApi
    {
        Task<PiuGameGetSongsResult> Get20AboveSongs(MixEnum mix, int page, CancellationToken cancellationToken);

        Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(MixEnum mix, string songId,
            CancellationToken cancellationToken);

        Task<PiuGameGetLeaderboardListResult> GetLeaderboards(MixEnum mix, CancellationToken cancellationToken);

        Task<PiuGameGetLeaderboardResult> GetLeaderboard(MixEnum mix, string leaderboardId,
            CancellationToken cancellationToken);

        Task<PiuGameGetChartPopularityLeaderboardResult> GetChartPopularityLeaderboard(MixEnum mix, int page,
            CancellationToken cancellationToken);

        Task<IEnumerable<PiuGameGetRecentScoresResult>> GetRecentScores(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task<(HttpClient client, string sid)> GetSessionId(MixEnum mix, string username, string password,
            CancellationToken cancellationToken);

        Task<PiuGameGetBestScoresResult>
            GetBestScores(MixEnum mix, HttpClient client, int page, CancellationToken cancellationToken);

        Task<PiuGameGetAccountDataResult> GetAccountData(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task<PiuGameGetUcsResult?> GetUcs(int ucsId, CancellationToken cancellationToken);

        Task<IEnumerable<GameCardRecord>> GetCards(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task SetCard(MixEnum mix, HttpClient client, string id, CancellationToken cancellationToken);
    }
}
