using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     What a session is on every read that shows one: a player's plays in one mix until eight hours
///     pass with no play (docs/design/session-breakdown.md D52–D53).
///     <para>
///         Stored sessions are narrower on purpose. An import run, a CSV upload, one API request and a
///         manual-entry envelope each keep their own row and id, because Undo, restart recovery and
///         each import's Discord card all work one stored session at a time. Before this, the page
///         showed those rows as they were stored, so five imports in an hour were five sessions. The
///         fold joins them back together for reading and stores nothing.
///     </para>
/// </summary>
internal static class SessionFold
{
    /// <summary>
    ///     How long a player can go without a play before the next one starts a new session. The
    ///     manual-entry envelope (<c>PlayerScoreBatchAccumulator</c>) mints its sessions on the same
    ///     silence, so a session made on the write side and one folded on the read side end alike.
    /// </summary>
    public static readonly TimeSpan QuietGap = TimeSpan.FromHours(8);

    /// <summary>
    ///     Folds a player's stored keys into sessions, per mix. A key joins the open session when its
    ///     first play lands within <see cref="QuietGap" /> of the latest play the session holds so far;
    ///     an overlap is a gap of zero, and exactly eight hours still joins, as it does in the envelope.
    ///     Unordered: callers sort by whatever their page reads.
    /// </summary>
    public static IReadOnlyList<FoldedSession> Fold(IEnumerable<StoredSessionKey> keys)
    {
        var sessions = new List<FoldedSession>();
        foreach (var mix in keys.GroupBy(k => k.Mix))
        {
            var open = new List<StoredSessionKey>();
            var openEnd = DateTimeOffset.MinValue;
            foreach (var key in mix.OrderBy(k => k.Start).ThenBy(k => k.End))
            {
                // Measured from the latest play the session holds, not from the previous key's last
                // play: a long import can end after a short one that started later, and the session
                // stays open until its latest play goes quiet.
                if (open.Count > 0 && key.Start - openEnd > QuietGap)
                {
                    sessions.Add(Close(mix.Key, open));
                    open = new List<StoredSessionKey>();
                }

                openEnd = open.Count == 0 || key.End > openEnd ? key.End : openEnd;
                open.Add(key);
            }

            if (open.Count > 0) sessions.Add(Close(mix.Key, open));
        }

        return sessions;
    }

    private static FoldedSession Close(MixEnum mix, IReadOnlyList<StoredSessionKey> members)
    {
        var stored = members.Where(m => m.SessionId != null).ToArray();
        // The handle is the newest stored session (D54): the one a player most recently heard from,
        // and the id the page's own links and View already carry.
        var newest = stored
            .OrderBy(m => m.End)
            .ThenBy(m => m.Start)
            .ThenBy(m => m.SessionId)
            .LastOrDefault();
        return new FoldedSession(mix,
            members.Min(m => m.Start),
            members.Max(m => m.End),
            newest?.SessionId,
            stored.Select(m => m.SessionId!.Value).ToArray(),
            members.Where(m => m.Day != null).Select(m => m.Day!.Value).ToArray());
    }
}

/// <summary>
///     One stored stretch of a player's journal as the fold reads it: a stored session, or, for rows
///     predating session capture, one mix's calendar day. <see cref="Start" /> and <see cref="End" />
///     are its first and last play.
/// </summary>
internal sealed record StoredSessionKey(Guid? SessionId, DateOnly? Day, MixEnum Mix, DateTimeOffset Start,
    DateTimeOffset End);

/// <summary>
///     A session as reads show it. <see cref="SessionIds" /> is every stored session folded in,
///     oldest first; <see cref="Days" /> is every pre-capture day folded in. <see cref="SessionId" />
///     is the newest stored session, or null when the session holds pre-capture days only.
/// </summary>
internal sealed record FoldedSession(
    MixEnum Mix,
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid? SessionId,
    IReadOnlyList<Guid> SessionIds,
    IReadOnlyList<DateOnly> Days)
{
    /// <summary>
    ///     The newest pre-capture day, naming a session that has no stored id to name it, as a day
    ///     bucket always did. Null once a stored session is in the fold.
    /// </summary>
    public DateOnly? Day => SessionId == null && Days.Count > 0 ? Days.Max() : null;
}
