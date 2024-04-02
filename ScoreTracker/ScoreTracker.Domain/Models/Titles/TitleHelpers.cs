using ScoreTracker.Domain.Models.Titles.Phoenix;

namespace ScoreTracker.Domain.Models.Titles
{
    public static class TitleHelpers
    {
        private sealed record OrderedTitle(TitleProgress t, int i)
        {
        }

        public static TitleProgress GetPushingTitle(this IEnumerable<TitleProgress> allTitles)
        {
            var titles = allTitles
                .Where(title => title.Title is PhoenixDifficultyTitle)
                .OrderBy(title => (title.Title as PhoenixDifficultyTitle)!.Level)
                .ThenBy(title => title.Title.Name)
                .ToArray();

            var firstAchieved = titles.Count() - (titles.Reverse().Select((t, i) => new OrderedTitle(t, i))
                .FirstOrDefault(t => t.t.CompletionCount >= t.t.Title.CompletionRequired)?.i ?? titles.Count());

            return titles[firstAchieved];
        }
    }
}
