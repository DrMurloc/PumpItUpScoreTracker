namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     Marks a test that spends real money against the Claude API. Manual runs only, never CI:
///     every execution bills tokens to the owner's account, and a suite that quietly re-runs on
///     every push would drain it.
///     <para>
///         Configure <c>ClaudeApi:ApiKey</c> in the shared user-secrets store (the Aspire
///         AppHost's) or the <c>ANTHROPIC_API_KEY</c> environment variable to run. Inert
///         otherwise, like the PIU and Discord probes beside it.
///     </para>
/// </summary>
public sealed class TranslationProbeFactAttribute : FactAttribute
{
    public TranslationProbeFactAttribute()
    {
        if (!TranslationProbeConfiguration.KeyConfigured)
            Skip = "Translation probe: configure the ClaudeApi:ApiKey user-secret (AppHost store) " +
                   "or the ANTHROPIC_API_KEY env var. Running this spends real tokens.";
    }
}
