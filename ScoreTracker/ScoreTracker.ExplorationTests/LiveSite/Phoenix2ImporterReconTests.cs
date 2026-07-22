using System.Text;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Phoenix 2 import-path canaries plus the page-snapshot instrument. The canaries mirror
///     <see cref="PiuGameLiveSiteTests" /> for the piugame.com host and the redesigned
///     my_page shapes (dated best cards, judgement tables); they go red the day the Phoenix 2
///     site drifts. The snapshot instrument dumps the raw pages — Phoenix 1 equivalents
///     included — to %TEMP%\p2-importer-recon (override with PIU_RECON_DUMP_DIR) for offline
///     shape diffing whenever the canaries do go red.
///     <para>All facts share one Phoenix 2 login through the fixture.</para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2ImporterReconTests : IClassFixture<PiuGameSessionFixture>
{
    private static readonly string DumpDir =
        Environment.GetEnvironmentVariable("PIU_RECON_DUMP_DIR")
        ?? Path.Combine(Path.GetTempPath(), "p2-importer-recon");

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2ImporterReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Phoenix2_account_data_parses_with_titles()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);

        var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix2, client, CancellationToken.None);

        Assert.NotEqual("INVALID", account.AccountName.ToString());
        Assert.True(account.ImageUrl.IsAbsoluteUri, "Profile image url did not parse.");
        Assert.NotEmpty(account.TitleEntries);
    }

    [LiveSiteFact]
    public async Task Phoenix2_game_cards_parse_with_exactly_one_active()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);

        var cards = (await _fixture.Api.GetCards(MixEnum.Phoenix2, client, CancellationToken.None)).ToList();

        Assert.NotEmpty(cards);
        Assert.Equal(1, cards.Count(c => c.IsActive));
    }

    [LiveSiteFact]
    public async Task Phoenix2_best_scores_parse_dated_and_newest_first()
    {
        // The redesigned my_best_score page: every card carries a saved datetime and the
        // list sorts newest-first — the incremental import's cutoff depends on both.
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);

        var result = await _fixture.Api.GetBestScores(MixEnum.Phoenix2, client, 1, CancellationToken.None);

        Assert.NotEmpty(result.Scores);
        Assert.All(result.Scores, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.SongName.ToString()), "Song name did not parse.");
            Assert.NotNull(s.RecordedAt);
        });
        Assert.Equal(result.Scores.Select(s => s.RecordedAt!.Value).OrderByDescending(d => d),
            result.Scores.Select(s => s.RecordedAt!.Value));
        // A page of all-SinglePerformance is the chart-type regex silently failing.
        Assert.Contains(result.Scores, s => s.ChartType != ChartType.SinglePerformance);
        Assert.All(result.Scores.Where(s => !s.IsBroken), s => Assert.NotNull(s.Plate));
        Assert.All(result.Scores.Where(s => s.IsBroken), s => Assert.Null(s.Plate));
    }

    [LiveSiteFact]
    public async Task Phoenix2_recent_scores_parse_with_judgements_and_dates()
    {
        // Requires at least one recent (non-stage-break) play on the account — the parser
        // drops unparseable cards silently, so empty-when-you-played-recently means breakage.
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);

        var recents = (await _fixture.Api.GetRecentScores(MixEnum.Phoenix2, client, CancellationToken.None))
            .ToList();

        Assert.NotEmpty(recents);
        Assert.All(recents, r =>
        {
            Assert.True(r.NoteCount > 0, "Judgement counts did not parse.");
            Assert.NotNull(r.RecordedAt);
        });
    }

    [LiveSiteFact]
    public async Task Page_snapshot_instrument_dumps_both_sites_my_pages()
    {
        Directory.CreateDirectory(DumpDir);
        var summary = new StringBuilder();
        var failedFetches = new List<string>();
        var ct = CancellationToken.None;

        var p2 = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        const string p2Base = "https://piugame.com";
        await Dump(p2, $"{p2Base}/my_page/title.php", "p2-title.html", summary, failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/game_id_information.php", "p2-game-cards.html", summary, failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/my_best_score.php?page=1", "p2-best-scores-page1.html", summary,
            failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/my_best_score.php?page=2", "p2-best-scores-page2.html", summary,
            failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/recently_played.php", "p2-recently-played.html", summary, failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/pumbility.php", "p2-pumbility.html", summary, failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/play_data.php", "p2-play-data.html", summary, failedFetches, ct);
        await Dump(p2, $"{p2Base}/my_page/my_best_score.php?lv=17", "p2-best-scores-lv17.html", summary, failedFetches,
            ct);

        var p1 = await _fixture.GetAuthenticatedClient(ct);
        const string p1Base = "https://phoenix.piugame.com";
        await Dump(p1, $"{p1Base}/my_page/my_best_score.php?page=1", "p1-best-scores-page1.html", summary,
            failedFetches, ct);
        await Dump(p1, $"{p1Base}/my_page/recently_played.php", "p1-recently-played.html", summary, failedFetches, ct);

        var summaryText = summary.ToString();
        await File.WriteAllTextAsync(Path.Combine(DumpDir, "summary.txt"), summaryText, ct);
        _output.WriteLine(summaryText);
        _output.WriteLine($"Snapshots: {DumpDir}");

        Assert.Empty(failedFetches);
    }

    private async Task Dump(HttpClient client, string url, string fileName, StringBuilder summary,
        List<string> failedFetches, CancellationToken ct)
    {
        try
        {
            var html = await client.GetStringAsync(url, ct);
            await File.WriteAllTextAsync(Path.Combine(DumpDir, fileName), html, ct);
            summary.AppendLine($"Fetched {fileName}: {html.Length:N0} chars");
        }
        catch (Exception e)
        {
            summary.AppendLine($"FETCH FAILED {fileName} ({url}): {e.Message}");
            failedFetches.Add(fileName);
        }
    }
}
