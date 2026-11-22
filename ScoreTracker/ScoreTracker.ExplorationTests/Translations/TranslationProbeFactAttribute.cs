namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     Marks a test that spends real money against the Claude API. Manual runs only, never CI:
///     every execution bills tokens to the owner's account, and a suite that quietly re-runs on
///     every push would drain it.
///     <para>
///         Runs only when <c>PIU_EXPLORE</c> names <c>translations</c> and <c>ClaudeApi:ApiKey</c>
///         is set in the shared user-secrets store (the Aspire AppHost's). Inert otherwise, like
///         the PIU and Discord probes beside it.
///     </para>
/// </summary>
public sealed class TranslationProbeFactAttribute : FactAttribute
{
    public TranslationProbeFactAttribute()
    {
        if (!ExploreOptIn.Names(ExploreOptIn.Translations))
            Skip = ExploreOptIn.SkipReason("Translation probe (spends real tokens)", ExploreOptIn.Translations);
        else if (!TranslationProbeConfiguration.KeyConfigured)
            Skip = "Translation probe: configure the ClaudeApi:ApiKey user-secret (AppHost store). " +
                   "Running this spends real tokens.";
    }
}
