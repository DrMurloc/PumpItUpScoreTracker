using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     What a session is on every read that shows one: a player's imports in one mix until eight
///     hours pass without one (docs/design/session-breakdown.md D52–D53).
///     <para>
///         Stored sessions are narrower on purpose. An import run, a CSV upload, one API request and a
///         manual-entry envelope each keep their own row and id, because Undo, restart recovery and
///         each import's Discord card all work one stored session at a time. Before this, the page
///         showed those rows as they were stored, so five imports in an hour were five sessions. The
///         fold joins them back together for reading and stores nothing.
///     </para>
///     <para>
///         It groups <b>imports</b>, by when they ran (owner, 2026-09-23: "You're grouping imports.
///         play times typically should not matter to us"). Play times are the site's dates, and an
///         import can carry a play the site dated weeks back — a stage break dated to the chart's first
///         attempt — which, measured by play time, stretched one import across every night between.
///         A stored session with no recorded import time is never grouped: nothing is backfilled.
///     </para>
/// </summary>
internal static class SessionFold
{
    /// <summary>
    ///     How long a player can go without importing before the next import starts a new session. The
    ///     manual-entry envelope (<c>PlayerScoreBatchAccumulator</c>) mints its sessions on the same
    ///     silence, so a session made on the write side and one folded on the read side end alike.
    /// </summary>
    public static readonly TimeSpan QuietGap = TimeSpan.FromHours(8);

    /// <summary>
    ///     Folds a player's stored keys into sessions, per mix. A key with an import window joins the
    ///     open session when its import started within <see cref="QuietGap" /> of the latest import
    ///     activity the session holds so far; an overlap is a gap of zero, and exactly eight hours still
    ///     joins, as it does in the envelope. A key without one stands alone, as it always did.
    ///     Unordered: callers sort by whatever their page reads.
    /// </summary>
    public static IReadOnlyList<FoldedSession> Fold(IEnumerable<StoredSessionKey> keys)
    {
        var sessions = new List<FoldedSession>();
        foreach (var mix in keys.GroupBy(k => k.Mix))
        {
            sessions.AddRange(mix.Where(k => k.Imported == null).Select(k => Close(mix.Key, new[] { k })));

            var open = new List<StoredSessionKey>();
            var openEnd = DateTimeOffset.MinValue;
            foreach (var key in mix.Where(k => k.Imported != null)
                         .OrderBy(k => k.Imported!.StartedAt)
                         .ThenBy(k => k.Imported!.LastActivityAt))
            {
                var imported = key.Imported!;
                // Measured from the latest activity the session holds, not from the previous
                // import's: a long import can outlast a short one that started after it, and the
                // session stays open until its latest activity goes quiet.
                if (open.Count > 0 && imported.StartedAt - openEnd > QuietGap)
                {
                    sessions.Add(Close(mix.Key, open));
                    open = new List<StoredSessionKey>();
                }

                openEnd = open.Count == 0 || imported.LastActivityAt > openEnd ? imported.LastActivityAt : openEnd;
                open.Add(key);
            }

            if (open.Count > 0) sessions.Add(Close(mix.Key, open));
        }

        return sessions;
    }

    private static FoldedSession Close(MixEnum mix, IReadOnlyList<StoredSessionKey> members)
    {
        var stored = members.Where(m => m.SessionId != null).ToArray();
        // The handle is the newest import (D54): the one a player most recently heard from, and
        // the id the page's own links and View already carry.
        var newest = stored
            .OrderBy(m => m.Imported?.LastActivityAt)
            .ThenBy(m => m.Imported?.StartedAt)
            .ThenBy(m => m.LastPlay)
            .ThenBy(m => m.SessionId)
            .LastOrDefault();
        return new FoldedSession(mix,
            members.Min(m => m.FirstPlay),
            members.Max(m => m.LastPlay),
            newest?.SessionId,
            stored.Select(m => m.SessionId!.Value).ToArray(),
            members.Where(m => m.Day != null).Select(m => m.Day!.Value).ToArray());
    }
}

/// <summary>
///     When a stored session's import ran, by the wall clock — its <c>ScoreSession</c> row, from the
///     moment the run opened to its last batch drain. Distinct from the plays' own dates on purpose.
/// </summary>
internal sealed record ImportWindow(DateTimeOffset StartedAt, DateTimeOffset LastActivityAt);

/// <summary>
///     One stored stretch of a player's journal as the fold reads it: a stored session, or, for rows
///     predating session capture, one mix's calendar day. <see cref="FirstPlay" /> and
///     <see cref="LastPlay" /> are its plays' own dates, which the page orders and prints by;
///     <see cref="Imported" /> is when it was imported, which is what groups it, and is null wherever
///     no import time was recorded.
/// </summary>
internal sealed record StoredSessionKey(Guid? SessionId, DateOnly? Day, MixEnum Mix, DateTimeOffset FirstPlay,
    DateTimeOffset LastPlay, ImportWindow? Imported = null);

/// <summary>
///     A session as reads show it. <see cref="Start" /> and <see cref="End" /> are its first and
///     last play by the plays' own dates — the clock every session on the page is ordered and
///     filtered by, grouped or not. <see cref="SessionIds" /> is every stored session folded in,
///     oldest import first; <see cref="Days" /> is the pre-capture day it stands for, if it is one.
///     <see cref="SessionId" /> is the newest import, or null for a pre-capture day.
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
    ///     The pre-capture day, naming a session that has no stored id to name it, as a day bucket
    ///     always did. Null for a stored session.
    /// </summary>
    public DateOnly? Day => SessionId == null && Days.Count > 0 ? Days.Max() : null;
}
