using System.Text;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Phoenix 2 importer recon: drives every my_page read the score import depends on against
///     the REAL piugame.com using the test account (the first account with live Phoenix 2 score
///     data), validates the production parsers over that session, and snapshots the raw pages —
///     plus the Phoenix 1 equivalents — for offline shape diffing (saved dates, judgement
///     tables, ordering controls).
///     <para>
///         Dumps land in %TEMP%\p2-importer-recon (override with PIU_RECON_DUMP_DIR). Parser
///         failures are recorded in the summary rather than failing fast, so one broken parser
///         never costs the rest of the recon; the test only fails when a page cannot be
///         fetched at all.
///     </para>
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
    public async Task Full_import_path_recon_with_page_snapshots()
    {
        Directory.CreateDirectory(DumpDir);
        var summary = new StringBuilder();
        var failedFetches = new List<string>();
        var ct = CancellationToken.None;

        // ---- Phoenix 2: dedicated login (the shared fixture session is Phoenix 1) ----
        var (p2, _) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2, PiuGameSessionFixture.Username!,
            PiuGameSessionFixture.Password!, ct);
        summary.AppendLine("Phoenix 2 login: OK");

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

        // ---- Production parsers over the same Phoenix 2 session ----
        await Record(summary, "P2 GetAccountData", async () =>
        {
            var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix2, p2, ct);
            return $"AccountName={account.AccountName}, RequiresLogin={account.RequiresLogin}, " +
                   $"Titles={account.TitleEntries?.Length ?? 0}";
        });
        await Record(summary, "P2 GetCards", async () =>
        {
            var cards = (await _fixture.Api.GetCards(MixEnum.Phoenix2, p2, ct)).ToArray();
            return $"{cards.Length} card(s), active={cards.Count(c => c.IsActive)}";
        });
        await Record(summary, "P2 GetBestScores(page 1)", async () =>
        {
            var best = await _fixture.Api.GetBestScores(MixEnum.Phoenix2, p2, 1, ct);
            var types = string.Join(",", best.Scores.Select(s => s.ChartType).Distinct());
            var sample = string.Join(" | ",
                best.Scores.Take(5).Select(s => $"{s.SongName} {s.ChartType} {s.Level} {s.Score} {s.Plate}"));
            return $"MaxPage={best.MaxPage}, Count={best.Scores.Length}, Types=[{types}]\n    Sample: {sample}";
        });
        await Record(summary, "P2 GetRecentScores", async () =>
        {
            var recent = (await _fixture.Api.GetRecentScores(MixEnum.Phoenix2, p2, ct)).ToArray();
            var sample = string.Join(" | ",
                recent.Take(5).Select(r =>
                    $"{r.SongName} {r.ChartType} {r.Level} {r.Score} {r.Plate} broken={r.IsBroken} notes={r.NoteCount}"));
            return $"Count={recent.Length}, Broken={recent.Count(r => r.IsBroken)}\n    Sample: {sample}";
        });

        // ---- Phoenix 1 equivalents for shape comparison (shared fixture session) ----
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

    private static async Task Record(StringBuilder summary, string label, Func<Task<string>> probe)
    {
        try
        {
            summary.AppendLine($"{label}: {await probe()}");
        }
        catch (Exception e)
        {
            summary.AppendLine($"{label}: THREW {e.GetType().Name}: {e.Message}");
        }
    }
}
