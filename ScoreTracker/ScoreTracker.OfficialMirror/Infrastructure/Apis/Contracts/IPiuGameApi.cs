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
        /// <summary>
        ///     On the Phoenix 2 site this page is login-gated — pass an authenticated
        ///     client (service login) for Phoenix 2; Phoenix stays anonymous (null).
        /// </summary>
        Task<PiuGameGetSongsResult> Get20AboveSongs(MixEnum mix, int page, CancellationToken cancellationToken,
            HttpClient? client = null);

        /// <summary>
        ///     The Phoenix 2 PUMBILITY ranking (login-gated — always needs an authenticated
        ///     client). Tab: null = All, Single = the ?t=s board, Double = ?t=d.
        /// </summary>
        Task<PiuGameGetPumbilityRankingResult> GetPumbilityRankings(MixEnum mix, ChartType? chartType, int page,
            HttpClient client, CancellationToken cancellationToken);

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

        // Rebuilds an authenticated client from a session id minted earlier by GetSessionId (no
        // network) — lets a single login serve many calls, including from a background job that
        // only carries the sid.
        HttpClient ClientForSid(MixEnum mix, string sid);

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
