using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Components;

/// <summary>What happened to the chart the player asked about.</summary>
public enum MixChangeVerdict
{
    /// <summary>The chart they named changed level.</summary>
    Moved,

    /// <summary>The chart they named is still where it was. This is an answer, not an absence.</summary>
    Unchanged,

    /// <summary>The whole song is new in the later mix.</summary>
    Arrived,

    /// <summary>The whole song is gone from the later mix.</summary>
    Departed,

    /// <summary>They named a song, not a chart, and some of its charts moved.</summary>
    SongMoved,

    /// <summary>They named a song and nothing on it moved.</summary>
    SongUnchanged
}

/// <summary>
///     The lookup's answer for one song, built from both mixes' catalogs rather than from
///     the diff: the diff only carries what changed, and "nothing changed" is a thing this
///     page has to be able to say out loud.
/// </summary>
public sealed record MixChangesAnswerModel(
    Song Song,
    MixChangeVerdict Verdict,
    Chart? PinnedBefore,
    Chart? PinnedAfter,
    IReadOnlyList<MixDiffMoveRecord> Moves,
    IReadOnlyList<Chart> Unchanged,
    IReadOnlyList<Chart> Gained,
    IReadOnlyList<Chart> Lost,
    int TotalCharts)
{
    public static MixChangesAnswerModel For(Chart selected,
        IReadOnlyDictionary<Name, Chart[]> beforeBySong,
        IReadOnlyDictionary<Name, Chart[]> afterBySong)
    {
        var name = selected.Song.Name;
        var before = beforeBySong.TryGetValue(name, out var b) ? b : Array.Empty<Chart>();
        var after = afterBySong.TryGetValue(name, out var a) ? a : Array.Empty<Chart>();

        var beforeById = before.ToDictionary(c => c.Id);
        var afterById = after.ToDictionary(c => c.Id);

        var moves = afterById.Values
            .Where(c => beforeById.ContainsKey(c.Id) && beforeById[c.Id].Level != c.Level)
            .Select(c => new MixDiffMoveRecord(beforeById[c.Id], c))
            .OrderBy(m => m.After.Type).ThenBy(m => m.After.Level)
            .ToArray();
        var unchanged = afterById.Values
            .Where(c => beforeById.ContainsKey(c.Id) && beforeById[c.Id].Level == c.Level)
            .OrderBy(c => c.Type).ThenBy(c => c.Level)
            .ToArray();
        var gained = afterById.Values.Where(c => !beforeById.ContainsKey(c.Id))
            .OrderBy(c => c.Type).ThenBy(c => c.Level).ToArray();
        var lost = beforeById.Values.Where(c => !afterById.ContainsKey(c.Id))
            .OrderBy(c => c.Type).ThenBy(c => c.Level).ToArray();

        // The selector hands back one chart, so the answer can name that chart specifically
        // rather than summarising the song.
        var pinnedBefore = beforeById.GetValueOrDefault(selected.Id);
        var pinnedAfter = afterById.GetValueOrDefault(selected.Id);

        var verdict = VerdictFor(before, after, pinnedBefore, pinnedAfter, moves.Length);
        var song = (after.FirstOrDefault() ?? before.FirstOrDefault() ?? selected).Song;

        return new MixChangesAnswerModel(song, verdict, pinnedBefore, pinnedAfter, moves, unchanged,
            gained, lost, Math.Max(before.Length, after.Length));
    }

    private static MixChangeVerdict VerdictFor(IReadOnlyList<Chart> before, IReadOnlyList<Chart> after,
        Chart? pinnedBefore, Chart? pinnedAfter, int moveCount)
    {
        if (before.Count == 0) return MixChangeVerdict.Arrived;
        if (after.Count == 0) return MixChangeVerdict.Departed;
        if (pinnedBefore != null && pinnedAfter != null)
            return pinnedBefore.Level == pinnedAfter.Level
                ? MixChangeVerdict.Unchanged
                : MixChangeVerdict.Moved;
        // The pinned chart exists on only one side, so the song survived but that chart
        // did not (or did not exist yet) — the song-level summary is the honest answer.
        return moveCount > 0 ? MixChangeVerdict.SongMoved : MixChangeVerdict.SongUnchanged;
    }
}
