using Microsoft.Playwright;

namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     Drives the real /PiuGameLogin form. The credentials are arbitrary — the WireMock
///     PIU site accepts anything and answers with the E2EPLAYER identity fixtures.
/// </summary>
internal static class PiuGameLoginFlow
{
    public const string Username = "e2euser";
    public const string Password = "correct-horse-battery";

    /// <summary>Signs in as a brand-new account; lands on the dashboard.</summary>
    public static async Task LogInAsNewUserAsync(IPage page)
    {
        await OpenFormAsync(page);
        await page.Locator("input[name='username']").FillAsync(Username);
        await page.Locator("input[name='password']").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log In" }).ClickAsync();
        // The dashboard's own empty state is the new-account landing now; "/" only resolves for
        // someone signed in, so arriving here at all is the proof the account exists.
        await page.WaitForURLAsync(u => new Uri(u).AbsolutePath == "/",
            new PageWaitForURLOptions { Timeout = 60_000 });

        await DismissAnnouncementsAsync(page);
    }

    /// <summary>
    ///     Clicks away any one-time rollout notice, the way the player who meets it does.
    ///     <para>
    ///         An account created through this flow is brand new, which is exactly who those notices
    ///         fire at. They are modal, so the scrim sits over the page the test is about and eats
    ///         every click — surfacing as a timeout waiting for a button that is plainly visible in
    ///         the trace. Seeded users get the same treatment from
    ///         <see cref="E2ESeedData.SeedUserAsync" />; this is the path that cannot be seeded,
    ///         because the account does not exist until the form is submitted.
    ///     </para>
    ///     <para>Tolerant by design: no notice pending is the normal case and not a failure.</para>
    /// </summary>
    public static async Task DismissAnnouncementsAsync(IPage page)
    {
        var acknowledge = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Got it" });
        try
        {
            await acknowledge.WaitForAsync(new LocatorWaitForOptions { Timeout = 5_000 });
        }
        catch (TimeoutException)
        {
            return;
        }

        await acknowledge.ClickAsync();
        await page.Locator("div.mud-overlay-scrim")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
    }

    /// <summary>Navigates to the form and fails with the served HTML if it never renders.</summary>
    public static async Task OpenFormAsync(IPage page)
    {
        var response = await page.GotoAsync("/PiuGameLogin");
        try
        {
            await page.Locator("input[name='username']")
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            // On the developer exception page, .titleerror/.location carry the real error.
            var summary = await page.EvaluateAsync<string>(
                "() => [document.querySelector('.titleerror')?.textContent, document.querySelector('.location')?.textContent, document.title].filter(Boolean).join(' | ')");
            throw new InvalidOperationException(
                $"The PIUGAME login form never rendered (HTTP {response?.Status}): {summary}");
        }
    }
}
