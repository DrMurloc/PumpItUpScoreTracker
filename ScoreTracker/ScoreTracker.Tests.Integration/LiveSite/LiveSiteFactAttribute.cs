namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Marks a test that talks to the real phoenix.piugame.com. These only run when a PIU
///     account is configured — either PiuTest:Username / PiuTest:Password in the shared
///     user-secrets store (the Aspire AppHost's) or the PIU_TEST_USERNAME / PIU_TEST_PASSWORD
///     environment variables. Unconfigured environments (CI included) skip them, so the suite
///     stays green for everyone while the owner can run the live canary on demand.
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
