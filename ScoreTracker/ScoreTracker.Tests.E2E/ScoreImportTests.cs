using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
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
        await PiuGameLoginFlow.LogInAsNewUserAsync(_page);

        await _page.GotoAsync("/UploadPhoenixScores");
        await FillMudFieldAsync("PIUGame.com Username", PiuGameLoginFlow.Username);
        await FillMudFieldAsync("PIUGame.com Password", PiuGameLoginFlow.Password);
        // The stubbed account has exactly one game card, so this single click resolves the
        // card and runs the whole import saga.
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Import", Exact = true })
            .ClickAsync();

        await Expect(_page.GetByText("Import Completed!"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 120_000 });

        // Both snapshot best scores streamed into the results table…
        await Expect(_page.GetByText("999,231")).ToBeVisibleAsync();
        await Expect(_page.GetByText("850,000")).ToBeVisibleAsync();

        // …and were persisted to the ledger, mapped onto the right charts.
        Assert.Equal(999231, await LedgerScoreFor(_fixture.Seed.Tricklash220Double20));
        Assert.Equal(850000, await LedgerScoreFor(_fixture.Seed.ConflictSingle15));
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
