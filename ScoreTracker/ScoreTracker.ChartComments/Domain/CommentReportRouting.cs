using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Which report reasons reach the site admin as well as the community's own moderators. The
///     deal: communities moderate themselves, public comments are the site admin's, and hate,
///     discrimination and threats escalate regardless. Internal — the routing is policy, and it is
///     deliberately not advertised to the reporter.
/// </summary>
internal static class CommentReportRouting
{
    public static bool EscalatesToSite(CommentReportReason reason) =>
        reason is CommentReportReason.HateOrDiscrimination or CommentReportReason.ThreatsOrHarassment;
}
