using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Where a report goes, decided by its reason and nothing else. The deal: communities moderate
///     themselves, public comments are the site admin's, and hate, discrimination and threats
///     escalate regardless. One reason sits outside the deal entirely — "I just want attention" is
///     the site admin's alone, never a community's, so a club's queue never fills with hellos.
///     Internal — the routing is policy, and it is deliberately not advertised to the reporter.
/// </summary>
internal static class CommentReportRouting
{
    /// <summary>Reaches the site admin from inside a community as well as its own moderators.</summary>
    public static bool EscalatesToSite(CommentReportReason reason) =>
        reason is CommentReportReason.HateOrDiscrimination or CommentReportReason.ThreatsOrHarassment;

    /// <summary>
    ///     Reaches ONLY the site admin, wherever the comment lives — never a community's desk. A
    ///     report with a site-only reason has no community slot to resolve, so its openness is the
    ///     site slot alone.
    /// </summary>
    public static bool IsSiteOnly(CommentReportReason reason) =>
        reason is CommentReportReason.JustWantAttention;

    /// <summary>Whether a community's own moderators ever see this report.</summary>
    public static bool ReachesCommunity(CommentReportReason reason) => !IsSiteOnly(reason);

    /// <summary>Whether the site admin sees a report with this reason on a COMMUNITY comment.</summary>
    public static bool ReachesSiteFromCommunity(CommentReportReason reason) =>
        EscalatesToSite(reason) || IsSiteOnly(reason);
}
