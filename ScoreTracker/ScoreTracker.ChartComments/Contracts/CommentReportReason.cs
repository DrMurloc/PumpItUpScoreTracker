namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     The closed reason vocabulary for reporting a comment. Closed on purpose: the reason decides
///     routing (community admins, the site admin as well, or the site admin alone), and a free-text
///     field cannot route. Which reasons go where is deliberately not part of this contract — the
///     reporter is never told whose desk a box reaches.
/// </summary>
public enum CommentReportReason
{
    SpamOrAdvertising,
    OffTopic,
    WrongInformation,
    HateOrDiscrimination,
    ThreatsOrHarassment,

    /// <summary>
    ///     "I just want attention. Hi." — the escape valve for someone who wants to be heard
    ///     rather than to report anything. Routed to the site admin ALONE, never to a community's
    ///     moderators, and kept in its own pile on the admin page so it never crowds a real report.
    /// </summary>
    JustWantAttention
}
