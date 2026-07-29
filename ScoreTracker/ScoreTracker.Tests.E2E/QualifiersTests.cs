using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The qualifiers ecosystem (docs/design/qualifiers-overhaul.md). Three facts only a hosted
///     run can establish: the two retired routes really 301 (a component redirect is a 302 and
///     would not consolidate), the board and the chart pool really render for an anonymous
///     visitor against a real database, and no photo URL reaches a player's response.
/// </summary>
[Collection("E2E")]
public sealed class QualifiersTests : IAsyncLifetime
{
    private const string PhotoUrl = "https://piu.test/qualifiers/secret-shot.png";

    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _tournamentId;

    public QualifiersTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();

        var hardChart = await _fixture.Seed.SeedPhoenixChartAsync("Napalm", 22, "Double");
        var easyChart = await _fixture.Seed.SeedPhoenixChartAsync("Bad Apple", 19, "Single");
        _tournamentId = await _fixture.Seed.SeedTournamentAsync("E2E Qualifier Cup");
        await _fixture.Seed.SeedQualifiersConfigurationAsync(_tournamentId, new[] { hardChart, easyChart });

        // One manual submission (photo-backed) and one imported (no photo owed).
        await _fixture.Seed.SeedQualifierEntryAsync(_tournamentId, "E2ELEADER", hardChart, 990_000,
            photoUrl: PhotoUrl);
        await _fixture.Seed.SeedQualifierEntryAsync(_tournamentId, "E2ERIVAL", easyChart, 950_000);

        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task TheRetiredSubmitRouteMovesPermanentlyToTheOnePage()
    {
        var response = await _page.APIRequest.GetAsync(
            $"{_fixture.BaseUrl}/Tournament/{_tournamentId}/Qualifiers/Submit",
            new APIRequestContextOptions { MaxRedirects = 0 });

        // 301, not 302: the submit page is gone for good and the signal should consolidate.
        Assert.Equal(301, response.Status);
        Assert.Equal($"/Tournament/{_tournamentId}/Qualifiers", response.Headers["location"]);
    }

    [Fact]
    public async Task TheLongDeadTournamentAdminLinkNowLandsOnTheQualifiersAdmin()
    {
        var response = await _page.APIRequest.GetAsync(
            $"{_fixture.BaseUrl}/Tournament/{_tournamentId}/Admin",
            new APIRequestContextOptions { MaxRedirects = 0 });

        // This route was rendered as a button for years while no page declared it.
        Assert.Equal(301, response.Status);
        Assert.Equal($"/Tournament/{_tournamentId}/Qualifiers/Admin", response.Headers["location"]);
    }

    [Fact]
    public async Task AnAnonymousVisitorGetsTheBoardAndTheWholeChartPool()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Tournament/{_tournamentId}/Qualifiers");
        await _page.WaitForSelectorAsync(".qual-card");

        // The pool is the question the old page refused to answer without a username.
        Assert.Equal(2, await _page.Locator(".qual-card").CountAsync());
        await Assertions.Expect(_page.Locator(".qual-pool")).ToContainTextAsync("Napalm");
        await Assertions.Expect(_page.Locator(".qual-pool")).ToContainTextAsync("Bad Apple");

        // Both entrants placed, each carrying a chip for the chart it was built on.
        Assert.Equal(2, await _page.Locator(".olb-rank-card").CountAsync());
        Assert.Equal(2, await _page.Locator(".qual-chip").CountAsync());
        await Assertions.Expect(_page.Locator("body")).ToContainTextAsync("E2ELEADER");
    }

    [Fact]
    public async Task APlayersResponseNeverCarriesAnotherPlayersPhoto()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Tournament/{_tournamentId}/Qualifiers");
        await _page.WaitForSelectorAsync(".qual-card");

        // Photos are organiser reference. The board projection has no field to put one in, and
        // this is the assertion that stays true if somebody widens it.
        var html = await _page.ContentAsync();
        Assert.DoesNotContain(PhotoUrl, html);
        Assert.DoesNotContain("secret-shot", html);
    }

    [Fact]
    public async Task TheQualifiersAdminRefusesAVisitorWhoRunsNoTournament()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Tournament/{_tournamentId}/Qualifiers/Admin");
        await _page.WaitForSelectorAsync(".mud-alert");

        // Authorization lives in the handler, so an anonymous visitor gets a refusal rather
        // than the field — and certainly rather than the photos.
        Assert.Equal(0, await _page.Locator(".qual-entry").CountAsync());
        var html = await _page.ContentAsync();
        Assert.DoesNotContain(PhotoUrl, html);
    }
}
