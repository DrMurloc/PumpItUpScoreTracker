using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using ScoreTracker.Web.Services;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

[Collection("E2E")]
public sealed class ScoreImportTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public ScoreImportTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedSnapshotCatalogAsync();
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task ImportingFromPiuGameStreamsProgressAndPersistsTheSnapshotScores()
    {
        // The seeded charts and the captured snapshot are Phoenix, and the import runs against
        // the account's current mix — so setup picks Phoenix rather than its Phoenix 2 default.
        await PiuGameLoginFlow.LogInAsNewUserAsync(_page, "Phoenix");

        await _page.GotoAsync("/UploadPhoenixScores");
        await FillMudFieldAsync("PIUGame.com Username", PiuGameLoginFlow.Username);
        await FillMudFieldAsync("PIUGame.com Password", PiuGameLoginFlow.Password);
        var import = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Import", Exact = true });

        // The captured account has two game cards, so the first click loads the cards,
        // auto-selects the active one, and asks for confirmation — the real multi-card flow.
        await import.ClickAsync();
        await Expect(_page.Locator("div.mud-input-control", new PageLocatorOptions { HasText = "Game Card" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await import.ClickAsync();

        // Two of the captured best scores streamed into the results table. The scores are the
        // completion signal — waiting on the status text instead would tie this to whatever
        // wording the import happens to report, which is what broke it last time.
        await Expect(_page.GetByText("999,231").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 120_000 });
        await Expect(_page.GetByText("1,000,000").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 120_000 });

        // …and were persisted to the ledger, mapped onto the right charts.
        Assert.Equal(999231, await LedgerScoreFor(_fixture.Seed.Tricklash220Double20));
        Assert.Equal(1000000, await LedgerScoreFor(_fixture.Seed.BluishRoseDouble18));
    }

    [Fact]
    public async Task TheConsoleScriptReadsAPhoenix2BestListIntoPhoenix2Records()
    {
        // The manual import end to end on the redesigned best list: the script the page hands out
        // runs in a browser against two trimmed pages of real markup, its CSV goes back through the
        // upload panel, and the records land in Phoenix 2 — Doubles still Doubles, the finished fail
        // broken, the stage break nowhere, and page 2 read because page 1's pager said so.
        var iolite = await _fixture.Seed.SeedPhoenix2ChartAsync("Iolite Sky", 21, "Double");
        var comeToMe = await _fixture.Seed.SeedPhoenix2ChartAsync("Come to Me", 18, "Single");
        var kugutsu = await _fixture.Seed.SeedPhoenix2ChartAsync("KUGUTSU", 25, "Single");
        var stager = await _fixture.Seed.SeedPhoenix2ChartAsync("STAGER", 17, "Single");
        var dreamchasers = await _fixture.Seed.SeedPhoenix2ChartAsync("Dreamchasers", 20, "Double");
        var tbh = await _fixture.Seed.SeedPhoenix2ChartAsync("T.B.H", 20, "Single");
        _fixture.ClearCaches();

        await PiuGameLoginFlow.LogInAsNewUserAsync(_page, "Phoenix 2");
        _fixture.PiuGame.Reset();
        _fixture.PiuGame.MapPhoenix2BestScorePages();

        var site = await _browser.NewPageAsync();
        await site.GotoAsync(_fixture.PiuGame.Urls[0] + "/");
        var download = await site.RunAndWaitForDownloadAsync(() =>
            site.EvaluateAsync(PiuGameConsoleScript.For(_fixture.PiuGame.Urls[0], 1, null)));
        var csv = Path.Combine(Path.GetTempPath(), $"piu-scores-{Guid.NewGuid():N}.csv");
        await download.SaveAsAsync(csv);
        var written = await File.ReadAllTextAsync(csv);
        Assert.Contains("\"Iolite Sky\",D21,995140,sss+,mg,false", written);
        Assert.Contains("\"KUGUTSU\",S25,706197,x_b,,true", written);
        Assert.DoesNotContain("STAGER", written);

        await _page.GotoAsync("/UploadPhoenixScores");
        await _page.GetByText("Manual import — console script + CSV").ClickAsync();
        await _page.Locator("#uploadInput").SetInputFilesAsync(csv);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save Scores" })
            .ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
        await Expect(_page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Restart" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 120_000 });
        File.Delete(csv);

        Assert.Equal((995140, false, true), await Phoenix2RecordFor(iolite));
        Assert.Equal((992910, false, true), await Phoenix2RecordFor(comeToMe));
        Assert.Equal((706197, true, false), await Phoenix2RecordFor(kugutsu));
        Assert.Null(await Phoenix2RecordFor(stager));
        Assert.Equal((977647, false, true), await Phoenix2RecordFor(dreamchasers));
        Assert.Equal((974936, false, true), await Phoenix2RecordFor(tbh));
    }

    private async Task FillMudFieldAsync(string label, string value)
    {
        var input = _page.Locator("div.mud-input-control", new PageLocatorOptions { HasText = label })
            .Locator("input");
        await input.FillAsync(value);
        // MudTextField's two-way binding commits on the change event (blur) — without it
        // the Blazor circuit never sees the value and the Import button stays disabled.
        await input.BlurAsync();
    }

    private async Task<(int Score, bool IsBroken, bool HasPlate)?> Phoenix2RecordFor(Guid chartId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Score, IsBroken, CASE WHEN Plate IS NULL THEN 0 ELSE 1 END FROM [scores].[PhoenixRecord] " +
                              "WHERE ChartId = @chartId AND MixId = @mixId";
        command.Parameters.AddWithValue("@chartId", chartId);
        command.Parameters.AddWithValue("@mixId", E2ESeedData.Phoenix2MixId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetInt32(0), reader.GetBoolean(1), reader.GetInt32(2) == 1);
    }

    private async Task<int?> LedgerScoreFor(Guid chartId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Score FROM [scores].[PhoenixRecord] WHERE ChartId = @chartId";
        command.Parameters.AddWithValue("@chartId", chartId);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : (int)result;
    }
}
