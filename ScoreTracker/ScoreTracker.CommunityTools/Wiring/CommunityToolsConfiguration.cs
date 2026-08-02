namespace ScoreTracker.CommunityTools.Wiring;

/// <summary>Host-supplied settings for the Community Tools vertical.</summary>
public sealed class CommunityToolsConfiguration
{
    public const string SectionName = "CommunityTools";

    /// <summary>
    ///     Whether a webhook may point at loopback or a private network.
    ///     <para>
    ///         False everywhere except the Aspire local run, which sets it — running under Aspire is
    ///         already this codebase's local-dev signal (it is what turns on AutoMigrate and the
    ///         DevAuth backdoor). A maker who wants to test against localhost runs the site locally,
    ///         which is what HOW-TO-RUN already tells them to do.
    ///     </para>
    /// </summary>
    public bool AllowPrivateWebhookTargets { get; set; }
}
