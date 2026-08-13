namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     The closed reason vocabulary for reporting a comment. Closed on purpose: the reason decides
///     routing (community admins, or the site admin as well), and a free-text field cannot route.
///     Which reasons escalate is deliberately not part of this contract — the reporter is never
///     told whose desk a box reaches.
/// </summary>
public enum CommentReportReason
{
    SpamOrAdvertising,
    OffTopic,
    WrongInformation,
    HateOrDiscrimination,
    ThreatsOrHarassment
}
