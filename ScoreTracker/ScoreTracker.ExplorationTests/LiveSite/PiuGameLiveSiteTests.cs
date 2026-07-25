using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Live canary for the PIU site scraper: every method the score-import flow depends on,
///     exercised against the REAL phoenix.piugame.com with a real account. This is the suite
///     that goes red the day PIU changes their HTML, image hosts, or login flow (like the
///     2026-07-03 piugame.com → phoenix.piugame.com image-host move that broke every import).
///     <para>
///         Gated on PIU_TEST_USERNAME / PIU_TEST_PASSWORD environment variables — skipped
///         everywhere they aren't set, including CI. Run on demand with:
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter "FullyQualifiedName~PiuGameLiveSiteTests"</c>
///     </para>
///     <para>
///         Assertions are shape-and-sanity based (parsers produce validated value types, so
///         "it parsed at all" carries most of the weight) — live data changes, golden values
///         belong in the offline approval fixtures. GetUcs is deliberately not covered: it
///         needs a known-stable UCS id and is not part of the account-import path.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PiuGameLiveSiteTests : IClassFixture<PiuGameSessionFixture>
{
    private readonly PiuGameSessionFixture _fixture;

    public PiuGameLiveSiteTests(PiuGameSessionFixture fixture)
    {
        _fixture = fixture;
    }

    [LiveSiteFact]
    public async Task Login_produces_a_session_and_account_data_parses()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);

        var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix, client, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(account.AccountName.ToString()), "Account name did not parse.");
        Assert.NotEqual("INVALID", account.AccountName.ToString());
        Assert.True(account.ImageUrl.IsAbsoluteUri, "Profile image url did not parse.");
    }

    [LiveSiteFact]
    public async Task Game_cards_list_the_accounts_cards_with_exactly_one_active()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);

        var cards = (await _fixture.Api.GetCards(MixEnum.Phoenix, client, CancellationToken.None)).ToList();

        Assert.NotEmpty(cards);
        Assert.Equal(1, cards.Count(c => c.IsActive));
        Assert.All(cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Id)));
    }

    [LiveSiteFact]
    public async Task Best_scores_parse_after_reselecting_the_active_card()
    {
        // The real import iterates cards via SetCard then scrapes best scores per card.
        // Re-selecting the already-active card exercises that endpoint without changing
        // any account state.
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);
        var cards = (await _fixture.Api.GetCards(MixEnum.Phoenix, client, CancellationToken.None)).ToList();
        var activeCard = Assert.Single(cards, c => c.IsActive);

        await _fixture.Api.SetCard(MixEnum.Phoenix, client, activeCard.Id, CancellationToken.None);
        var result = await _fixture.Api.GetBestScores(MixEnum.Phoenix, client, 1, CancellationToken.None);

        // The per-card try/catch swallows parse failures, so an empty page here means the
        // parser is silently dropping every card — exactly the 2026-07-03 incident shape.
        Assert.True(result.MaxPage >= 1, "Pagination did not parse.");
        Assert.NotEmpty(result.Scores);
        Assert.All(result.Scores, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.SongName.ToString()), "Song name did not parse.");
            Assert.InRange((int)s.Score, 0, 1_000_000);
        });
    }

    [LiveSiteFact]
    public async Task Recent_scores_parse_for_the_account()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);

        var recents = (await _fixture.Api.GetRecentScores(MixEnum.Phoenix, client, CancellationToken.None)).ToList();

        // Requires the account to have at least one recent play — the parser drops
        // unparseable cards silently, so empty-when-you-played-yesterday means breakage.
        Assert.NotEmpty(recents);
        Assert.All(recents, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.SongName.ToString()), "Song name did not parse.");
            Assert.True(r.NoteCount > 0, "Note counts did not parse.");
        });
    }

    [LiveSiteFact]
    public async Task Over_20_songs_and_their_song_leaderboards_parse()
    {
        var songs = await _fixture.Api.Get20AboveSongs(MixEnum.Phoenix, 1, CancellationToken.None);

        Assert.NotEmpty(songs.Results);
        Assert.All(songs.Results, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name), "Song name did not parse.");
            Assert.False(string.IsNullOrWhiteSpace(s.Id), "Song leaderboard id did not parse.");
        });

        var leaderboard =
            await _fixture.Api.GetSongLeaderboard(MixEnum.Phoenix, songs.Results.First().Id, page: 1,
                CancellationToken.None);

        Assert.NotEmpty(leaderboard.Results);
        // Phoenix boards serve whole in one page — paging icons appearing here would mean
        // the site changed shape and the sweep's walk assumptions need a fresh look.
        Assert.True(leaderboard.IsEnd, "Phoenix board unexpectedly paginates now.");
        Assert.All(leaderboard.Results, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.ProfileName), "Profile name did not parse.");
            Assert.InRange(e.Score, 0, 1_000_000);
        });
    }

    [LiveSiteFact]
    public async Task Rating_leaderboard_list_and_a_leaderboard_parse()
    {
        var leaderboards = await _fixture.Api.GetLeaderboards(MixEnum.Phoenix, CancellationToken.None);

        Assert.NotEmpty(leaderboards.Entries);
        Assert.All(leaderboards.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));

        var withId = leaderboards.Entries.First(e => !string.IsNullOrWhiteSpace(e.Id));
        var leaderboard = await _fixture.Api.GetLeaderboard(MixEnum.Phoenix, withId.Id, CancellationToken.None);

        Assert.NotEmpty(leaderboard.Entries);
        Assert.All(leaderboard.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.ProfileName), "Profile name did not parse.");
            Assert.True(e.Rating > 0, "Rating did not parse.");
        });
    }

    [LiveSiteFact]
    public async Task Chart_popularity_leaderboard_parses()
    {
        var result = await _fixture.Api.GetChartPopularityLeaderboard(MixEnum.Phoenix, 1, DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.NotEmpty(result.Entries);
        Assert.All(result.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.SongName.ToString()), "Song name did not parse.");
            Assert.True(e.Place > 0, "Placement did not parse.");
            Assert.True(e.ChartType is ChartType.Single or ChartType.Double or ChartType.CoOp
                    or ChartType.SinglePerformance or ChartType.DoublePerformance,
                "Chart type did not parse.");
        });
    }
}
