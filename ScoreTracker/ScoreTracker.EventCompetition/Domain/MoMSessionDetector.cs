using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     A journalled play, reduced to what deciding "was this a session?" needs: when the machine
///     recorded it, how long the song runs, which board it could belong to, and whether the stage
///     ended it. The handler builds these by joining the score journal to the catalog — the journal
///     knows the time, only the catalog knows the duration.
/// </summary>
internal sealed record MoMPlay(
    Guid ChartId,
    DateTimeOffset PlayedAt,
    TimeSpan Duration,
    ChartType Type,
    bool IsStageBroken)
{
    /// <summary>The play ends when its song does; the journal stamps the start.</summary>
    public DateTimeOffset EndsAt => PlayedAt + Duration;
}

/// <summary>
///     A contiguous run of plays with no long break inside it. Indexes point back into the list the
///     block was split out of, because the dialog draws every play — inside the selection or not —
///     and moves the selection by index (§11.4).
/// </summary>
internal sealed record MoMPlayBlock(int StartIndex, int EndIndex, DateTimeOffset From, DateTimeOffset To, int Plays);

/// <summary>
///     A stretch of the night that looks like a session (D32): the plays it holds, all of one chart
///     type, and how much of the window was rest. Indexes point into the list it was found in.
/// </summary>
internal sealed record MoMSessionWindow(
    int StartIndex,
    int EndIndex,
    ChartType Type,
    int Charts,
    TimeSpan SongTime,
    TimeSpan Rest);

/// <summary>The break the soft warning names: how long, and the play that came after it.</summary>
internal sealed record MoMBreak(TimeSpan Length, Guid BeforeChartId, DateTimeOffset BeforePlayedAt);

/// <summary>
///     What D10's three checks say about one selected range, plus the two counts the dialog prints
///     under them.
/// </summary>
internal sealed record MoMRangeChecks(
    int Charts,
    TimeSpan SongTime,
    bool OverWindowBeforeLast,
    TimeSpan Span,
    bool SpanOverWindow,
    MoMBreak? LongestBreak,
    int StageBreaksSkipped,
    int WrongTypeSkipped,
    int RepeatPlays);

/// <summary>
///     Finding a session inside a night of plays, as pure functions over the journal's timestamps.
///     Two questions are asked of the same data and they are not the same scan.
///     <para>
///         <b>The import dialog</b> (§11.4) has to turn an undifferentiated list of recent plays into
///         a choosable session, so it splits at the long gaps: a session is contiguous and its
///         boundaries are the breaks either side. <see cref="Split" /> draws the blocks,
///         <see cref="Suggest" /> opens on one and <see cref="Check" /> runs D10's three checks over
///         whatever the player selects.
///     </para>
///     <para>
///         <b>The My Sessions callout</b> (D32) asks something narrower and never splits: is there a
///         window the length of a session anywhere in this night whose plays are one chart type and
///         whose rest is under fifty minutes? <see cref="FindSessionWindow" /> slides that window,
///         because the night's own start and end do not matter.
///     </para>
/// </summary>
internal static class MoMSessionDetector
{
    /// <summary>A gap longer than this ends a block. Long enough to outlast a queue, short enough to catch a night's end.</summary>
    public static readonly TimeSpan BlockGap = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Splits plays into blocks wherever the machine sat idle longer than <see cref="BlockGap" />.
    ///     Expects them oldest first; a caller holding the journal's newest-first order reverses it.
    /// </summary>
    public static IReadOnlyList<MoMPlayBlock> Split(IReadOnlyList<MoMPlay> plays)
    {
        if (plays.Count == 0) return Array.Empty<MoMPlayBlock>();

        var blocks = new List<MoMPlayBlock>();
        var start = 0;
        for (var i = 1; i <= plays.Count; i++)
        {
            if (i < plays.Count && GapBefore(plays, i) <= BlockGap) continue;

            blocks.Add(new MoMPlayBlock(start, i - 1, plays[start].PlayedAt, plays[i - 1].EndsAt, i - start));
            start = i;
        }

        return blocks;
    }

    /// <summary>
    ///     The idle time between a play and the one before it: from when the earlier song ended to
    ///     when the next one started. Never negative — overlapping stamps mean a duration we do not
    ///     trust, not time travel.
    /// </summary>
    public static TimeSpan GapBefore(IReadOnlyList<MoMPlay> plays, int index)
    {
        var gap = plays[index].PlayedAt - plays[index - 1].EndsAt;
        return gap < TimeSpan.Zero ? TimeSpan.Zero : gap;
    }

    /// <summary>
    ///     The block the dialog opens on: the one that would put the most charts on the board, with
    ///     the most recent winning a tie. A night usually has one obvious block and several strays,
    ///     and this picks the obvious one without hiding the rest.
    /// </summary>
    public static MoMPlayBlock? Suggest(IReadOnlyList<MoMPlay> plays, ChartType type, TimeSpan window)
    {
        MoMPlayBlock? best = null;
        var bestCharts = 0;
        foreach (var block in Split(plays))
        {
            var checks = Check(plays, block.StartIndex, block.EndIndex, type, window);
            if (checks.Charts == 0 || checks.OverWindowBeforeLast) continue;
            if (best != null && checks.Charts < bestCharts) continue;

            best = block;
            bestCharts = checks.Charts;
        }

        return best;
    }

    /// <summary>
    ///     Runs D10's three checks over a selected range. Song time over the window before the last
    ///     chart is a hard block, because the window governs when a chart may <em>start</em> (§1) and
    ///     the closing chart is allowed to overhang it. Wall-clock span over the window is only a
    ///     warning: it is a judgement call, so it names the longest break rather than gesturing at
    ///     one — telling someone to trim an end is useless when the break is in the middle.
    /// </summary>
    public static MoMRangeChecks Check(IReadOnlyList<MoMPlay> plays, int startIndex, int endIndex,
        ChartType type, TimeSpan window, TimeSpan alreadyUsed = default,
        IReadOnlySet<Guid>? alreadyHeld = null)
    {
        var (from, to) = Clamp(plays, startIndex, endIndex);
        var selected = Range(plays, from, to);
        var counted = selected.Where(p => Counts(p, type)).ToArray();

        // The budget is what the resulting session would spend, not what the night took: a session
        // holds each chart once (D45 replaces in place), and a chart the draft already holds spends
        // nothing at all, because entering it again is a swap. Counting either twice blocks a
        // selection that would have fitted.
        var seen = new HashSet<Guid>();
        var fresh = counted.Where(p => seen.Add(p.ChartId) && alreadyHeld?.Contains(p.ChartId) != true)
            .ToArray();
        var songTime = alreadyUsed + Sum(fresh);
        // Running time only grows, so the one add that can find the window full is the last new
        // chart — which makes the time before it the whole test.
        var beforeLast = fresh.Length == 0 ? alreadyUsed : songTime - fresh[^1].Duration;
        var overBeforeLast = fresh.Length > 0 && beforeLast >= window;

        var span = selected.Count == 0
            ? TimeSpan.Zero
            : selected[^1].EndsAt - selected[0].PlayedAt;
        var spanOver = span > window && !overBeforeLast;

        // A repeat is a play whose chart is already in: within one mix a chart id is the whole
        // identity (song, type and level), which is what D45 compares.
        var distinct = counted.Select(p => p.ChartId).Distinct().Count();
        var repeats = counted.Length - distinct;

        return new MoMRangeChecks(
            distinct,
            songTime,
            overBeforeLast,
            span,
            spanOver,
            spanOver ? LongestBreak(plays, from, to) : null,
            selected.Count(p => p.IsStageBroken),
            selected.Count(p => !p.IsStageBroken && p.Type != type),
            repeats);
    }

    /// <summary>
    ///     D32's scan, and the My Sessions callout's whole question: is there a stretch of this night
    ///     the length of a session in which every counted play is one chart type and the rest is
    ///     under <paramref name="maxRest" />?
    ///     <para>
    ///         The window slides — a night's own start and end do not matter, because a night can
    ///         carry plays a skipped import left behind and a session can sit anywhere inside it.
    ///         Plays of the other chart type are not excluded from the window, only from the count:
    ///         their time becomes rest, which is what makes a mixed night fail honestly.
    ///     </para>
    ///     <para>
    ///         Returns the fullest window found, or null when nothing qualifies. On the two nights
    ///         this was built against: a public player's 8 August Doubles run fills 61.5 minutes of a
    ///         105-minute window with 31 charts and 43.5 minutes of rest, and qualifies; DrMurloc's
    ///         14 August night tops out at 28.2 minutes of song and 76.8 of rest, and does not.
    ///     </para>
    /// </summary>
    public static MoMSessionWindow? FindSessionWindow(IReadOnlyList<MoMPlay> plays, TimeSpan window,
        TimeSpan maxRest)
    {
        MoMSessionWindow? best = null;
        foreach (var type in new[] { ChartType.Double, ChartType.Single })
        {
            var ofType = Enumerable.Range(0, plays.Count).Where(i => Counts(plays[i], type)).ToArray();
            for (var from = 0; from < ofType.Length; from++)
            {
                var opened = plays[ofType[from]].PlayedAt;
                var song = TimeSpan.Zero;
                for (var to = from; to < ofType.Length; to++)
                {
                    // A chart only has to START inside the window (§1), so the last one may overhang.
                    if (plays[ofType[to]].PlayedAt - opened > window) break;

                    song += plays[ofType[to]].Duration;
                    if (best != null && song <= best.SongTime) continue;

                    var rest = window - song;
                    best = new MoMSessionWindow(ofType[from], ofType[to], type, to - from + 1, song,
                        rest < TimeSpan.Zero ? TimeSpan.Zero : rest);
                }
            }
        }

        return best != null && best.Rest < maxRest ? best : null;
    }

    private static MoMBreak? LongestBreak(IReadOnlyList<MoMPlay> plays, int startIndex, int endIndex)
    {
        MoMBreak? longest = null;
        for (var i = startIndex + 1; i <= endIndex; i++)
        {
            var gap = GapBefore(plays, i);
            if (longest != null && gap <= longest.Length) continue;

            longest = new MoMBreak(gap, plays[i].ChartId, plays[i].PlayedAt);
        }

        return longest;
    }

    private static IReadOnlyList<MoMPlay> Range(IReadOnlyList<MoMPlay> plays, int from, int to) =>
        plays.Count == 0 ? Array.Empty<MoMPlay>() : plays.Skip(from).Take(to - from + 1).ToArray();

    /// <summary>
    ///     The selection, held inside the list. The dialog moves an end by index and a caller may
    ///     hand over a stale one, so every read of the range goes through this — including the break
    ///     scan, which used to walk the caller's own bounds and could read past the end.
    /// </summary>
    private static (int From, int To) Clamp(IReadOnlyList<MoMPlay> plays, int startIndex, int endIndex)
    {
        if (plays.Count == 0) return (0, -1);

        var from = Math.Clamp(startIndex, 0, plays.Count - 1);
        return (from, Math.Clamp(endIndex, from, plays.Count - 1));
    }

    /// <summary>A play counts when the stage did not end it and it belongs to this board's chart type.</summary>
    private static bool Counts(MoMPlay play, ChartType type) => !play.IsStageBroken && play.Type == type;

    private static TimeSpan Sum(IReadOnlyList<MoMPlay> plays) =>
        TimeSpan.FromTicks(plays.Sum(p => p.Duration.Ticks));
}
