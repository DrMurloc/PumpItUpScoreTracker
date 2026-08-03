namespace ScoreTracker.CommunityTools.Contracts;

/// <summary>
///     Whether a tool touches player scores at all.
///     <para>
///         Stated by the maker at registration rather than derived from whether keys exist: a
///         brand-new integrated tool has none either, for the thirty seconds before its maker mints
///         one, and the directory would offer players a Visit button for a tool that reads scores.
///     </para>
/// </summary>
public enum ToolKind
{
    /// <summary>Reads scores. Has an API key, and may receive deliveries.</summary>
    Integrated,

    /// <summary>
    ///     A directory entry pointing at a site. No key, no API, no webhooks, and nothing for a
    ///     player to grant — which is why its row offers Visit rather than Connect.
    /// </summary>
    ListingOnly
}

/// <summary>Where a tool sits in the listing flow.</summary>
public enum ToolVisibility
{
    /// <summary>Fully functional and reachable by invite. The starting state, and a valid resting one.</summary>
    Private,

    /// <summary>Listing requested, awaiting an admin decision. Still fully functional.</summary>
    PendingApproval,

    /// <summary>In the directory, and eligible for players who share with all tools.</summary>
    Public,

    /// <summary>Listing refused with a reason. Still fully functional as a private tool.</summary>
    Rejected
}

/// <summary>
///     What we send a tool when one of its players imports.
///     <para>
///         The first three are one tier: none of them carries power the tool's API key does not
///         already have, so a maker moves between them freely. <see cref="PiuGameSession" /> is a
///         different tier — it hands over a live piugame.com credential — and entering it is gated.
///     </para>
/// </summary>
public enum WebhookMode
{
    /// <summary>No delivery. The tool polls, or reads the event feed.</summary>
    None,

    /// <summary>"This player imported", and nothing else.</summary>
    PlayerPing,

    /// <summary>The changed scores themselves, chunked, with a link to the rest.</summary>
    ScorePush,

    /// <summary>
    ///     The piugame.com session key we signed in with, so the tool runs its own scrape. Gives the
    ///     tool control of the player's PIUGame account for as long as the session lives.
    /// </summary>
    PiuGameSession
}

/// <summary>How a player's access to a tool came about.</summary>
public enum ShareSource
{
    /// <summary>The player connected to this tool specifically.</summary>
    Direct,

    /// <summary>The player shares with every approved tool, and this one accepts that pool.</summary>
    AllTools
}

/// <summary>Where a delivery attempt got to.</summary>
public enum DeliveryStatus
{
    Pending,
    Succeeded,

    /// <summary>Failed, and a retry is scheduled.</summary>
    Failed,

    /// <summary>Failed on the final attempt. No further retries.</summary>
    Abandoned
}

/// <summary>
///     Why a delivery failed, as a closed vocabulary rather than free text.
///     <para>
///         The activity console shows these to tool makers, and a maker-facing surface is not an
///         admin page — raw exception text has no business there. A curated reason plus the remote's
///         own status code says everything a maker needs and nothing about our internals.
///     </para>
/// </summary>
public enum WebhookFailureReason
{
    None,
    Timeout,
    DnsFailure,
    TlsFailure,

    /// <summary>The remote answered 4xx. Its status code rides alongside.</summary>
    ClientError,

    /// <summary>The remote answered 5xx.</summary>
    ServerError,

    /// <summary>Connected, but the answer was not something we could accept.</summary>
    InvalidResponse
}
