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

    /// <summary>
    ///     Whether a webhook may point at anything <b>outside</b> loopback and the private ranges.
    ///     <para>
    ///         True everywhere except the Aspire local run, which sets it false — the mirror image of
    ///         <see cref="AllowPrivateWebhookTargets" />. A local database is a copy of production, and
    ///         a copy of production holds real tools with real endpoints and real deliveries still
    ///         queued: the startup import-recovery pass alone would replay hundreds of sessions into
    ///         them from a developer's laptop. With this false, the one client every outbound webhook
    ///         POST goes through refuses a public target before a byte leaves, and records the
    ///         delivery as abandoned rather than retrying it. Loopback and private targets still
    ///         deliver, which is what a maker running the site locally is there for.
    ///     </para>
    /// </summary>
    public bool AllowPublicWebhookTargets { get; set; } = true;
}
