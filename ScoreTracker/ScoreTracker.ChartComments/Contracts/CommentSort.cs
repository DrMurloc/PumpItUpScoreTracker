namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     How a chart's roots are ordered. Top is the default; the Notes scope offers no choice at all,
///     because with no votes there is only one order.
/// </summary>
public enum CommentSort
{
    /// <summary>Votes descending, then newest — ties broken toward the thing nobody has seen yet.</summary>
    Top,

    Newest
}

/// <summary>
///     Why a comment is showing as a stub. A deleted comment only leaves one when a reply hangs off
///     it; otherwise a thread of four fills with headstones for comments nobody answered.
/// </summary>
public enum CommentDeletion
{
    ByAuthor,
    ByModerator,

    /// <summary>
    ///     The account is gone. The row survives with no author and no text, purely to hold the
    ///     shape of a thread its replies are still part of.
    /// </summary>
    ByDeletedAccount
}
