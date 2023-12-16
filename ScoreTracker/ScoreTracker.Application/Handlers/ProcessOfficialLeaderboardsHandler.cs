using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class ProcessOfficialLeaderboardsHandler : IRequestHandler<ProcessOfficialLeaderboardsCommand>
    {
        private readonly IOfficialSiteClient _officialSite;
        private readonly ITierListRepository _tierLists;
        private readonly IOfficialLeaderboardRepository _leaderboards;

        public ProcessOfficialLeaderboardsHandler(IOfficialSiteClient officialSite, ITierListRepository tierLists,
            IOfficialLeaderboardRepository leaderboards)
        {
            _officialSite = officialSite;
            _tierLists = tierLists;
            _leaderboards = leaderboards;
        }

        public async Task<Unit> Handle(ProcessOfficialLeaderboardsCommand request, CancellationToken cancellationToken)
        {
            var leaderboardEntries = (await _officialSite.GetLeaderboardEntries(cancellationToken)).ToArray();
            foreach (var leaderboard in leaderboardEntries.GroupBy(l => l.LeaderboardName))
            {
                await _leaderboards.ClearLeaderboard("Rating", leaderboard.Key, cancellationToken);
                var place = 1;
                foreach (var scoreGroup in leaderboard.GroupBy(l => l.Score).OrderByDescending(kv => kv.Key))
                {
                    var currentPlace = place;
                    foreach (var entry in scoreGroup)
                    {
                        await _leaderboards.WriteEntry(entry with { Place = currentPlace }, cancellationToken);
                        place++;
                    }
                }
            }

            var scores = (await _officialSite.GetAllOfficialChartScores(CancellationToken.None)).ToArray();

            await PopulateTierLists(scores, cancellationToken);
            await SaveUserLeaderboards(scores, cancellationToken);
            return Unit.Value;
        }

        private async Task SaveUserLeaderboards(IEnumerable<OfficialChartLeaderboardEntry> entries,
            CancellationToken cancellationToken)
        {
            foreach (var group in entries.GroupBy(e => e.Chart.Id))
            {
                var leaderboardName = group.First().Chart.Song.Name + " " + group.First().Chart.DifficultyString;
                await _leaderboards.ClearLeaderboard("Chart", leaderboardName, cancellationToken);
                var place = 1;
                foreach (var scoreGroup in group.GroupBy(e => (int)e.Score).OrderByDescending(g => g.Key))
                {
                    var currentPlace = place;
                    foreach (var entry in scoreGroup)
                    {
                        await _leaderboards.WriteEntry(
                            new UserOfficialLeaderboard(entry.Username, currentPlace, "Chart", leaderboardName,
                                entry.Score),
                            cancellationToken);
                        place++;
                    }
                }
            }
        }

        private async Task PopulateTierLists(IEnumerable<OfficialChartLeaderboardEntry> entries,
            CancellationToken cancellationToken)
        {
            var entryArray = entries.ToArray();
            var averages = entryArray.GroupBy(c => c.Chart.Id)
                .ToDictionary(g => g.Key, g => (int)g.Average(e => e.Score));
            var charts = entryArray.Select(e => e.Chart).GroupBy(c => c.Id).Select(g => g.First());
            var levelGroup = charts.GroupBy(s => (s.Type, s.Level));
            var result = new List<SongTierListEntry>();
            foreach (var group in levelGroup)
            {
                var scores = group.Select(g => averages[g.Id]).ToArray();
                var orders = group.OrderByDescending(s => averages[s.Id]).Select((s, i) => (s, i))
                    .ToDictionary(kv => kv.s.Id, kv => kv.i);
                var average = (int)scores.Average();
                var standardDev = StdDev(scores, true);
                var mediumMin = average - standardDev / 2;
                var easyMin = average + standardDev / 2;
                var veryEasyMin = average + standardDev;
                var oneLevelOverrated = average + standardDev * 1.5;
                var hardMin = average - standardDev;
                var veryHardMin = average - standardDev * 1.5;
                foreach (var song in group)
                {
                    var score = averages[song.Id];
                    var category = TierListCategory.Unrecorded;
                    if (score == -1)
                        category = TierListCategory.Unrecorded;
                    else if (score < veryHardMin)
                        category = TierListCategory.Underrated;
                    else if (score < hardMin)
                        category = TierListCategory.VeryHard;
                    else if (score < mediumMin)
                        category = TierListCategory.Hard;
                    else if (score < easyMin)
                        category = TierListCategory.Medium;
                    else if (score < veryEasyMin)
                        category = TierListCategory.Easy;
                    else if (score < oneLevelOverrated)
                        category = TierListCategory.VeryEasy;
                    else
                        category = TierListCategory.Overrated;


                    result.Add(new SongTierListEntry("Official Scores", song.Id, category,
                        orders[song.Id]));
                }
            }

            foreach (var r in result) await _tierLists.SaveEntry(r, cancellationToken);
        }

        public static double StdDev(IEnumerable<int> values,
            bool as_sample)
        {
            // Get the mean.
            double mean = values.Sum() / values.Count();

            // Get the sum of the squares of the differences
            // between the values and the mean.
            var squares_query =
                from int value in values
                select (value - mean) * (value - mean);
            var sum_of_squares = squares_query.Sum();

            if (as_sample)
                return Math.Sqrt(sum_of_squares / (values.Count() - 1));
            return Math.Sqrt(sum_of_squares / values.Count());
        }
    }
}
