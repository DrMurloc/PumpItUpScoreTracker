namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Marks a test that talks to the real phoenix.piugame.com. These only run when a PIU
///     account is configured via the PIU_TEST_USERNAME / PIU_TEST_PASSWORD environment
///     variables — unconfigured environments (CI included) skip them, so the suite stays
///     green for everyone while the owner can run the live canary on demand.
/// </summary>
public sealed class LiveSiteFactAttribute : FactAttribute
{
    public LiveSiteFactAttribute()
    {
        if (!PiuGameSessionFixture.CredentialsConfigured)
            Skip = "Live-site test: set PIU_TEST_USERNAME and PIU_TEST_PASSWORD to run against the real PIU site.";
    }
}
