namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     How a March of Murlocs board orders what is on it. Boards rank <b>sessions</b>, not
///     players (D16): one player may hold several places, in pure score order, and a tie goes
///     to the session published first (§1). Drafts never reach a board, so every session
///     ranked here has a publication time.
/// </summary>
internal static class MoMBoardRanking
{
    public static IReadOnlyList<T> Order<T>(IEnumerable<T> sessions, Func<T, int> totalScore,
        Func<T, DateTimeOffset> publishedAt)
    {
        return sessions.OrderByDescending(totalScore).ThenBy(publishedAt).ToArray();
    }

    /// <summary>
    ///     A session's place for one lever against every session on its board: one more than
    ///     the number of sessions that beat it, so equals share the better place. "Better" is
    ///     higher for charts, difficulty and grade, and lower for downtime.
    /// </summary>
    public static int LeverPlace(double mine, IEnumerable<double> board, bool higherIsBetter)
    {
        return 1 + board.Count(other => higherIsBetter ? other > mine : other < mine);
    }

    /// <summary>
    ///     Which of a player's sessions this is on the board, counting from their earliest
    ///     publication — the "2nd session" chip that tells two rows of one player apart (D18).
    /// </summary>
    public static int SessionNumber<T>(IEnumerable<T> sessions, T session, Func<T, Guid> userId,
        Func<T, DateTimeOffset> publishedAt) where T : notnull
    {
        var mine = sessions.Where(s => userId(s).Equals(userId(session))).OrderBy(publishedAt).ToList();
        return mine.IndexOf(session) + 1;
    }
}
