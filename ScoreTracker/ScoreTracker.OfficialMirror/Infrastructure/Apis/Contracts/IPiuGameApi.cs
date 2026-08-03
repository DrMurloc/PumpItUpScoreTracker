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
        ///     A mix's PUMBILITY ranking. Tab: null = All, Single = the ?t=s board, Double =
        ///     ?t=d — Phoenix ignores the tab and serves its single board for any of them.
        ///     Login-gated on Phoenix 2 (pass an authenticated service client); Phoenix stays
        ///     anonymous (null).
        /// </summary>
        Task<PiuGameGetPumbilityRankingResult> GetPumbilityRankings(MixEnum mix, ChartType? chartType, int page,
            HttpClient? client, CancellationToken cancellationToken);

        /// <summary>
        ///     One page of a chart board. Phoenix serves the whole board on page 1 (no paging
        ///     icons → IsEnd); Phoenix 2's deeper boards page with the same next/last-icon
        ///     protocol as the PUMBILITY board. The gated Phoenix 2 boards need the
        ///     authenticated client; Phoenix stays anonymous (null).
        /// </summary>
        Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(MixEnum mix, string songId, int page,
            CancellationToken cancellationToken, HttpClient? client = null);

        Task<PiuGameGetLeaderboardListResult> GetLeaderboards(MixEnum mix, CancellationToken cancellationToken);

        Task<PiuGameGetLeaderboardResult> GetLeaderboard(MixEnum mix, string leaderboardId,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The official play ranking for the month containing <paramref name="asOf" />.
        ///     Login-gated on Phoenix 2 like the rest of its ranking pages — pass the
        ///     authenticated client there; Phoenix stays anonymous (null).
        /// </summary>
        Task<PiuGameGetChartPopularityLeaderboardResult> GetChartPopularityLeaderboard(MixEnum mix, int page,
            DateTimeOffset asOf, CancellationToken cancellationToken, HttpClient? client = null);

        Task<IEnumerable<PiuGameGetRecentScoresResult>> GetRecentScores(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task<(HttpClient client, string sid)> GetSessionId(MixEnum mix, string username, string password,
            CancellationToken cancellationToken);

        // Rebuilds an authenticated client from a session id minted earlier by GetSessionId (no
        // network) — lets a single login serve many calls, including from a background job that
        // only carries the sid.
        HttpClient ClientForSid(MixEnum mix, string sid);

        /// <summary>
        ///     One page of the best-score list. <paramref name="bucket" /> applies the page's own
        ///     <c>?lv=</c> filter, which is what lets a repair read only the levels a census said
        ///     disagree instead of walking the whole account.
        /// </summary>
        Task<PiuGameGetBestScoresResult> GetBestScores(MixEnum mix, HttpClient client, int page,
            CancellationToken cancellationToken, string? bucket = null);

        /// <summary>
        ///     One <c>?lv=</c> bucket of the play-data page: how many charts the player has PASSED
        ///     there, broken down by grade and plate. Counts exclude stage breaks on both mixes,
        ///     which is what makes them comparable against our records regardless of whether the
        ///     player imports breaks. Pass "" for the whole account.
        /// </summary>
        Task<PiuGameGetPlayDataResult> GetPlayData(MixEnum mix, HttpClient client, string bucket,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The official PUMBILITY pool with the site's own per-chart values, read live — the
        ///     ranking board is a daily batch and lags a session behind.
        /// </summary>
        Task<PiuGameGetPumbilityResult> GetPumbility(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The charts behind one play-data count tile, six to a page.
        ///     <paramref name="isGrade" /> picks the grade cell over the plate one.
        /// </summary>
        Task<PiuGameGetPlayLogResult> GetPlayLog(MixEnum mix, HttpClient client, string bucket, string type,
            bool isGrade, int page, CancellationToken cancellationToken);

        Task<PiuGameGetAccountDataResult> GetAccountData(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task<PiuGameGetUcsResult?> GetUcs(int ucsId, CancellationToken cancellationToken);

        Task<IEnumerable<GameCardRecord>> GetCards(MixEnum mix, HttpClient client,
            CancellationToken cancellationToken);

        Task SetCard(MixEnum mix, HttpClient client, string id, CancellationToken cancellationToken);
    }
}
