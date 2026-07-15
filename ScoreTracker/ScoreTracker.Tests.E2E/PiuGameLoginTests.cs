using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

[Collection("E2E")]
public sealed class PiuGameLoginTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public PiuGameLoginTests(E2EAppFixture fixture)
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
    public async Task FirstPiuGameLoginCreatesTheAccountAndSignsTheBrowserIn()
    {
        await PiuGameLoginFlow.LogInAsNewUserAsync(_page);

        // A brand-new PIU identity lands on the dashboard — and "/" bounces anyone without an
        // account to the front door, so staying here is itself proof the sign-in took.
        Assert.Equal("/", new Uri(_page.Url).AbsolutePath);

        // …with an account created from the stubbed PIU profile (name = game tag)…
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        var created = await context.User.SingleOrDefaultAsync(u => u.Name == PiuGameStubs.GameTag);
        Assert.NotNull(created);

        // …and a real session cookie in the browser, so the next page load is authenticated.
        var cookies = await _browser.CookiesAsync();
        Assert.Contains(cookies, c => c.Name.Contains("DefaultAuthentication"));
    }

    [Fact]
    public async Task InvalidPiuGameCredentialsBounceBackToTheFormWithAnError()
    {
        // Take the login stubs down to the "wrong password" shape: piugame still answers,
        // but the account page has no profile — the app maps that to invalid credentials.
        _fixture.PiuGame.Reset();
        _fixture.PiuGame.MapPiuGameInvalidLogin();

        await PiuGameLoginFlow.OpenFormAsync(_page);
        await _page.Locator("input[name='username']").FillAsync("e2euser");
        await _page.Locator("input[name='password']").FillAsync("wrong-password");
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log In" }).ClickAsync();

        await Expect(_page.GetByText("Invalid username or password"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        Assert.EndsWith("/PiuGameLogin", new Uri(_page.Url).AbsolutePath);
    }
}
