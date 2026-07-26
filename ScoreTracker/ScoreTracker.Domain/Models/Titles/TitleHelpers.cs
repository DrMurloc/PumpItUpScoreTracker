using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles
{
    public static class TitleHelpers
    {
        private sealed record OrderedTitle(TitleProgress t, int i)
        {
        }

        /// <summary>
        ///     Links a ladder of titles that all measure the same pool at rising thresholds —
        ///     a difficulty folder's Lv.1/2/3, a PUMBILITY pool, the CO-OP rating ladder — so
        ///     each rung's progress measures the climb from the rung below it. Without this a
        ///     player who just earned Lv.1 reads as a third of the way to Lv.2 when they have
        ///     not moved at all.
        /// </summary>
        /// <param name="ladderKey">
        ///     What makes two titles the same ladder: the folder's level, the pumbility pool.
        /// </param>
        public static void LinkLadder<TTitle, TKey>(IEnumerable<TTitle> titles, Func<TTitle, TKey> ladderKey)
            where TTitle : Title
        {
            foreach (var ladder in titles.Where(t => t.CompletionRequired > 0).GroupBy(ladderKey))
            {
                var floor = 0;
                foreach (var title in ladder.OrderBy(t => t.CompletionRequired))
                {
                    title.FloorAt(floor);
                    floor = title.CompletionRequired;
                }
            }
        }

        /// <summary>
        ///     Places titles on their display rails, numbering rungs by declaration order
        ///     within each rail — the list reads top to bottom the way the page draws it.
        ///     A null key leaves a title off every rail (a one-off badge).
        /// </summary>
        /// <remarks>
        ///     Separate from <see cref="LinkLadder{TTitle,TKey}" /> on purpose: that groups by
        ///     what scoring shares, this by what the page draws, and they legitimately differ
        ///     (see <see cref="Title.Ladder" />).
        /// </remarks>
        public static void Rail<TTitle>(IEnumerable<TTitle> titles, Func<TTitle, Name?> railKey)
            where TTitle : Title
        {
            Rail(titles, railKey, _ => 0);
        }

        /// <summary>
        ///     As <see cref="Rail{TTitle}(IEnumerable{TTitle},Func{TTitle,Name?})" />, but numbering
        ///     rungs by an explicit order rather than by declaration. Rungs stay contiguous from 1
        ///     however sparse the rail is — a mix with only a double boss chart gets a rung 1, not
        ///     a rung 2 with a hole in front of it.
        /// </summary>
        public static void Rail<TTitle, TOrder>(IEnumerable<TTitle> titles, Func<TTitle, Name?> railKey,
            Func<TTitle, TOrder> rungOrder)
            where TTitle : Title
        {
            foreach (var rail in titles.Select(t => (title: t, key: railKey(t)))
                         .Where(x => x.key != null)
                         .GroupBy(x => x.key!.Value))
            {
                var rung = 1;
                foreach (var entry in rail.OrderBy(x => rungOrder(x.title)))
                    entry.title.OnRail(rail.Key, rung++);
            }
        }

        public static TitleProgress GetPushingTitle(this IEnumerable<TitleProgress> allTitles)
        {
            var titles = allTitles
                .Where(title => title.Title is PhoenixDifficultyTitle)
                .OrderBy(title => (title.Title as PhoenixDifficultyTitle)!.Level)
                .ThenBy(title => title.Title.CompletionRequired)
                .ToArray();

            var firstAchieved = titles.Count() - (titles.Reverse().Select((t, i) => new OrderedTitle(t, i))
                .FirstOrDefault(t => t.t.CompletionCount >= t.t.Title.CompletionRequired)?.i ?? titles.Count());

            return titles[firstAchieved];
        }
    }
}
