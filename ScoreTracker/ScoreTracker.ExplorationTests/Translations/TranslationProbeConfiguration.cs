using Microsoft.Extensions.Configuration;

namespace ScoreTracker.ExplorationTests.Translations;

/// <summary>
///     Where the translation probes get their API key. Shares the Aspire AppHost's user-secrets
///     store like the rest of this assembly, so one command configures it:
///     <c>dotnet user-secrets set "ClaudeApi:ApiKey" "sk-ant-..." --project ScoreTracker/ScoreTracker.AppHost</c>.
///     The environment variable wins when both are set.
///     <para>
///         The key is named for the service it opens, not for the feature that happens to use it
///         first — a secret whose name does not say what it is for is a secret nobody can audit.
///     </para>
/// </summary>
internal static class TranslationProbeConfiguration
{
    private static readonly Lazy<IConfigurationRoot> Configuration = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets<TranslationProbeFactAttribute>(true)
            .Build());

    public static string? ApiKey =>
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? Configuration.Value["ClaudeApi:ApiKey"];

    public static bool KeyConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Where sweep reports are written. Outside the repo — these are run artifacts, not source.</summary>
    public static string ReportDirectory =>
        Environment.GetEnvironmentVariable("SCORETRACKER_TRANSLATION_REPORTS")
        ?? Path.Combine(Path.GetTempPath(), "scoretracker-translation-probe");
}
