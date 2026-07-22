using ScoreTracker.Domain.Models.Titles.Phoenix;

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
