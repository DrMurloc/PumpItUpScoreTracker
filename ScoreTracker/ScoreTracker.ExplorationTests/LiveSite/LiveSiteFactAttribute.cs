namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Marks a test that talks to the real phoenix.piugame.com. Manual runs only, never the PR
///     gate — like the Discord canary, CI does not provision the PIU account, so these skip in
///     every automated build and a flaky real site can't fail it. Run them on demand when a change
///     touches the PIU scraper, by configuring PiuTest:Username / PiuTest:Password in the shared
///     user-secrets store (the Aspire AppHost's) or the PIU_TEST_USERNAME / PIU_TEST_PASSWORD
///     environment variables.
/// </summary>
public sealed class LiveSiteFactAttribute : FactAttribute
{
    public LiveSiteFactAttribute()
    {
        if (!PiuGameSessionFixture.CredentialsConfigured)
            Skip = "Live-site test: configure PiuTest:Username/PiuTest:Password user-secrets " +
                   "(AppHost store) or PIU_TEST_USERNAME/PIU_TEST_PASSWORD env vars to run against the real PIU site.";
    }
}
