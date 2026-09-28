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

    /// <summary>
    ///     A page that sends a logged-out visitor to sign in gets them back. An existing account
    ///     comes straight back; a brand-new one goes through setup first (the next test).
    /// </summary>
    [Fact]
    public async Task SigningInFromAPageReturnsTheVisitorToIt()
    {
        await PiuGameLoginFlow.LogInAsNewUserAsync(_page);

        var signedOut = await _fixture.NewBrowserContextAsync();
        try
        {
            var page = await signedOut.NewPageAsync();
            await page.GotoAsync("/Login?returnUrl=%2FAccount");

            var piuGame = page.Locator("a.btn[href^='/PiuGameLogin']");
            Assert.Equal("/PiuGameLogin?returnUrl=%2FAccount", await piuGame.GetAttributeAsync("href"));
            await piuGame.ClickAsync();

            await page.Locator("input[name='username']")
                .FillAsync(PiuGameLoginFlow.Username, new LocatorFillOptions { Timeout = 30_000 });
            await page.Locator("input[name='password']").FillAsync(PiuGameLoginFlow.Password);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log In" }).ClickAsync();

            await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/Account",
                new PageWaitForURLOptions { Timeout = 60_000 });
        }
        finally
        {
            await signedOut.DisposeAsync();
        }
    }

    /// <summary>
    ///     A brand-new account keeps the page it started from through setup, and Continue goes back
    ///     to it instead of the home page (docs/design/new-user-setup.md D13) — the path a player
    ///     signing up from a tool's invite link takes.
    /// </summary>
    [Fact]
    public async Task ANewAccountGoesBackToThePageItStartedFromAfterSetup()
    {
        await _page.GotoAsync("/Login?returnUrl=%2FCommunityTools");
        await _page.Locator("a.btn[href^='/PiuGameLogin']").ClickAsync();

        await _page.Locator("input[name='username']")
            .FillAsync(PiuGameLoginFlow.Username, new LocatorFillOptions { Timeout = 30_000 });
        await _page.Locator("input[name='password']").FillAsync(PiuGameLoginFlow.Password);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log In" }).ClickAsync();

        await _page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/Setup",
            new PageWaitForURLOptions { Timeout = 60_000 });
        await _page.Locator("button.setup-continue").ClickAsync(new LocatorClickOptions { Timeout = 60_000 });

        await _page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/CommunityTools",
            new PageWaitForURLOptions { Timeout = 60_000 });
    }

    [Fact]
    public async Task FirstPiuGameLoginCreatesTheAccountAndSignsTheBrowserIn()
    {
        await PiuGameLoginFlow.LogInAsNewUserAsync(_page);

        // A brand-new PIU identity walks setup and lands on the dashboard — and neither "/Setup"
        // nor "/" resolves without an account, so arriving here is itself proof the sign-in took.
        Assert.Equal("/", new Uri(_page.Url).AbsolutePath);

        // …with an account created from the stubbed PIU profile (name = game tag)…
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        var created = await context.User.SingleOrDefaultAsync(u => u.Name == PiuGameStubs.GameTag);
        Assert.NotNull(created);

        // …and a real session cookie in the browser, so the next page load is authenticated.
        var cookies = await _browser.CookiesAsync();
        Assert.Contains(cookies, c => c.Name.Contains("DefaultAuthentication"));
    }

    /// <summary>
    ///     The three-beat opening: sign in, set up, build. This pins the middle beat — that a new
    ///     account is actually taken there, that the page renders live rather than as an empty
    ///     circuit, and that the field that only exists because OAuth fills it in badly arrives
    ///     carrying its own provenance.
    /// </summary>
    [Fact]
    public async Task ANewAccountIsTakenToSetupBeforeTheDashboard()
    {
        await PiuGameLoginFlow.SignUpAsync(_page);

        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(_page.Locator("button.setup-continue")).ToBeVisibleAsync(timeout);
        // PIUGAME is the one provider that hands over a usable name — the game tag itself.
        await Expect(_page.Locator("#setup-username")).ToHaveValueAsync(PiuGameStubs.GameTag, new()
        {
            Timeout = 60_000
        });
        await Expect(_page.GetByText("filled in from PIUGAME")).ToBeVisibleAsync(timeout);

        // Public ships off, and the mix opens on the current game.
        Assert.Equal("false", await _page.Locator("button.setup-switch").GetAttributeAsync("aria-checked"));
        await Expect(_page.Locator("button.setup-gbtn[aria-pressed='true']")).ToHaveTextAsync("Phoenix 2", new()
        {
            Timeout = 60_000
        });
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
