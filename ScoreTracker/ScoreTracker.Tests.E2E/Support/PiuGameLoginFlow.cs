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

    /// <summary>Signs in as a brand-new account; lands on /Welcome.</summary>
    public static async Task LogInAsNewUserAsync(IPage page)
    {
        await OpenFormAsync(page);
        await page.Locator("input[name='username']").FillAsync(Username);
        await page.Locator("input[name='password']").FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Log In" }).ClickAsync();
        await page.WaitForURLAsync("**/Welcome", new PageWaitForURLOptions { Timeout = 60_000 });
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
