namespace ScoreTracker.Data.Configuration
{
    /// <summary>
    ///     The Claude API credential, named for the service it opens rather than the feature that
    ///     happens to use it first. Absent by default: with no key the batch client reports itself
    ///     unconfigured and the translation pipeline parks, which is the shipping posture until
    ///     configuration deliberately arms it.
    /// </summary>
    public sealed class ClaudeApiConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
