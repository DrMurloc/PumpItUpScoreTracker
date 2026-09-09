using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles;

/// <summary>
///     Which titles supersede which. A player standing on a rung of a ladder has, by definition,
///     cleared every rung below it — so a consumer that hands out one thing per ladder (Discord
///     roles, today) needs to know the ladder and the order, and needs both to come from the
///     shipped list rather than a copy of it.
/// </summary>
public static class TitleExclusivity
{
    /// <summary>
    ///     The group a title competes within, or null for a title that supersedes nothing.
    ///     <para>
    ///         <b><see cref="Title.Ladder" /> is deliberately not the answer here</b>, and it is
    ///         the obvious wrong one. <see cref="Phoenix2TitleList" /> rails a pool as three bands
    ///         of ten plus a capstone — <c>[S] INTERMEDIATE</c>, <c>[S] ADVANCED</c>,
    ///         <c>[S] EXPERT</c>, <c>[S] MASTER</c> — because thirty-one rungs on one line read as
    ///         nothing. Grouping on that would make exclusivity per BAND: reaching
    ///         <c>[S] EXPERT LV.1</c> would leave <c>[S] ADVANCED LV.10</c> standing.
    ///     </para>
    ///     <para>
    ///         The pool is the real ladder, which is exactly what
    ///         <see cref="TitleHelpers.LinkLadder{TTitle,TKey}" /> already groups those titles by
    ///         when it floors their progress.
    ///     </para>
    /// </summary>
    public static Name? GroupOf(Title title)
    {
        return title is Phoenix2PumbilityTitle pumbility ? (Name?)$"PUMBILITY {pumbility.Pool}" : null;
    }

    /// <summary>
    ///     Where a title stands inside its group — higher supersedes lower. The threshold IS the
    ///     order for a pool ladder, and it is the same number the title gates on, so a rung cannot
    ///     move here without moving there. Zero for a title in no group, which never competes.
    /// </summary>
    public static int RankIn(Title title)
    {
        return title is Phoenix2PumbilityTitle pumbility ? pumbility.CompletionRequired : 0;
    }

    /// <summary>
    ///     Thins a held set down to what should actually be worn: every title outside a group,
    ///     plus the single strongest rung of each group. Ties keep them all — nothing in the
    ///     shipped list ties, and silently dropping one would be worse than wearing two.
    /// </summary>
    public static IReadOnlyList<TTitle> HighestOnly<TTitle>(IEnumerable<TTitle> held,
        Func<TTitle, Title> resolve)
    {
        var all = held as IReadOnlyList<TTitle> ?? held.ToArray();
        var best = new Dictionary<Name, int>();
        foreach (var item in all)
        {
            var title = resolve(item);
            if (GroupOf(title) is not { } group) continue;
            var rank = RankIn(title);
            if (!best.TryGetValue(group, out var standing) || rank > standing) best[group] = rank;
        }

        return all.Where(item =>
        {
            var title = resolve(item);
            return GroupOf(title) is not { } group || RankIn(title) >= best[group];
        }).ToArray();
    }
}
