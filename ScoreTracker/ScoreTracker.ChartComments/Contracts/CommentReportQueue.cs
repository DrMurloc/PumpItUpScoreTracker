namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     Which desk a dismissal clears. Dismissal is per-queue by design: a community admin's
///     dismissal clears their panel and only theirs, while an escalated report stays with the site
///     admin until the site admin acts — escalation exists precisely for the club that won't.
/// </summary>
public enum CommentReportQueue
{
    Community,
    Site
}
