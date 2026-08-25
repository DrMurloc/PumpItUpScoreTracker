using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services.MoM;

/// <summary>
///     One importable play on the Submit page's timeline: the journal row joined to its
///     catalog chart, in play order (oldest first).
/// </summary>
public sealed record MoMImportPlay(ScoreJournalEntry Entry, Chart Chart, bool Selectable)
{
    public double Seconds => Chart.Song.Duration.TotalSeconds;
    public DateTimeOffset EndsAt => Entry.OccurredAt + Chart.Song.Duration;
}

/// <summary>
///     The gap-detection arithmetic behind the import dialog (march-of-murlocs.md §11.4).
///     The journal is an undifferentiated list of recent plays; a session is a contiguous
///     block, and its boundaries are the long gaps either side — so plays split wherever a
///     gap exceeds fifteen minutes, the longest block pre-selects (ties to the most recent),
///     and clicking any play moves whichever selection end is nearer. Pure — the dialog
///     renders exactly what this computes, and the tests pin it here rather than through the
///     renderer. There is deliberately no minimum block size: a one-chart session imports.
/// </summary>
public static class MoMSessionImport
{
    public static readonly TimeSpan GapBreak = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     The rest before play <paramref name="index" /> — wall clock from the previous
    ///     play's end to this one's start, null for the first play. Floors at zero: a journal
    ///     stamp can land inside the previous song's runtime when durations disagree.
    /// </summary>
    public static TimeSpan? GapBefore(IReadOnlyList<MoMImportPlay> plays, int index)
    {
        if (index <= 0 || index >= plays.Count) return null;
        var gap = plays[index].Entry.OccurredAt - plays[index - 1].EndsAt;
        return gap < TimeSpan.Zero ? TimeSpan.Zero : gap;
    }

    /// <summary>Index ranges of contiguous blocks, in play order.</summary>
    public static IReadOnlyList<(int Start, int End)> Blocks(IReadOnlyList<MoMImportPlay> plays)
    {
        if (plays.Count == 0) return Array.Empty<(int, int)>();
        var blocks = new List<(int Start, int End)>();
        var start = 0;
        for (var i = 1; i < plays.Count; i++)
            if (GapBefore(plays, i) > GapBreak)
            {
                blocks.Add((start, i - 1));
                start = i;
            }

        blocks.Add((start, plays.Count - 1));
        return blocks;
    }

    /// <summary>The longest block — the likeliest session — ties going to the most recent.</summary>
    public static (int Start, int End) BestBlock(IReadOnlyList<MoMImportPlay> plays)
    {
        var best = (Start: 0, End: -1);
        foreach (var block in Blocks(plays))
            if (block.End - block.Start >= best.End - best.Start)
                best = block;
        return best;
    }

    /// <summary>
    ///     Clicking a play moves whichever selection end is nearer — one click, no handles,
    ///     the same gesture on a phone.
    /// </summary>
    public static (int Start, int End) MoveNearestEnd((int Start, int End) selection, int index)
    {
        return Math.Abs(index - selection.Start) <= Math.Abs(index - selection.End)
            ? (Math.Min(index, selection.End), selection.End)
            : (selection.Start, Math.Max(index, selection.Start));
    }

    /// <summary>
    ///     D10's hard block: everything BEFORE the last selectable play must start inside the
    ///     window, so it is the song time excluding the final chart that cannot exceed it —
    ///     the closing chart may overhang (§2.9).
    /// </summary>
    public static bool ExceedsWindow(IReadOnlyList<MoMImportPlay> selection, TimeSpan maxTime)
    {
        var selectable = selection.Where(p => p.Selectable).ToArray();
        if (selectable.Length == 0) return false;
        var beforeLast = selectable.Take(selectable.Length - 1)
            .Sum(p => p.Seconds);
        return TimeSpan.FromSeconds(beforeLast) > maxTime;
    }

    /// <summary>
    ///     D10's soft warning names the culprit: the longest break inside the selection, and
    ///     the play it came before — telling someone to trim an end is useless when the break
    ///     is in the middle, where no end reaches it.
    /// </summary>
    public static (TimeSpan Gap, MoMImportPlay Before)? LongestBreakInside(
        IReadOnlyList<MoMImportPlay> plays, (int Start, int End) selection)
    {
        (TimeSpan Gap, MoMImportPlay Before)? longest = null;
        for (var i = selection.Start + 1; i <= selection.End; i++)
        {
            var gap = GapBefore(plays, i);
            if (gap != null && (longest == null || gap > longest.Value.Gap))
                longest = (gap.Value, plays[i]);
        }

        return longest;
    }
}
