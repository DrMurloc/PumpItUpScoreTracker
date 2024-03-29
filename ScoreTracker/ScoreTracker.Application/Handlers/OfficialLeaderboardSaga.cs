﻿using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class OfficialLeaderboardSaga : IRequestHandler<ProcessOfficialLeaderboardsCommand>,
        IRequestHandler<ProcessChartPopularityCommand>,
        IRequestHandler<ImportOfficialPlayerScoresCommand>
    {
        private readonly IOfficialSiteClient _officialSite;
        private readonly ITierListRepository _tierLists;
        private readonly IOfficialLeaderboardRepository _leaderboards;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IUserRepository _user;
        private readonly IMediator _mediator;
        private readonly IBus _bus;

        public OfficialLeaderboardSaga(IOfficialSiteClient officialSite, ITierListRepository tierLists,
            IOfficialLeaderboardRepository leaderboards, ICurrentUserAccessor currentUser, IUserRepository user,
            IMediator mediator,
            IBus bus)
        {
            _officialSite = officialSite;
            _tierLists = tierLists;
            _leaderboards = leaderboards;
            _currentUser = currentUser;
            _user = user;
            _mediator = mediator;
            _bus = bus;
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


                    await _tierLists.SaveEntry(new SongTierListEntry("Official Scores", song.Id, category,
                        orders[song.Id]), cancellationToken);
                }
            }
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

        public async Task<Unit> Handle(ProcessChartPopularityCommand request, CancellationToken cancellationToken)
        {
            var entries = await _officialSite.GetOfficialChartLeaderboardEntries(cancellationToken);
            foreach (var levelTypeGroup in entries.GroupBy(e => (e.Chart.Level, e.Chart.Type)))
            {
                var charts = levelTypeGroup.ToArray();
                var average = charts.Average(c => c.Place);
                var standardDev = StdDev(charts.Select(c => c.Place), true);
                var mediumMin = average - standardDev / 2;
                var easyMin = average + standardDev / 2;
                var veryEasyMin = average + standardDev;
                var oneLevelOverrated = average + standardDev * 1.5;
                var hardMin = average - standardDev;
                var veryHardMin = average - standardDev * 1.5;
                foreach (var (chart, score) in levelTypeGroup)
                {
                    var category = TierListCategory.Unrecorded;
                    if (score == -1)
                        category = TierListCategory.Unrecorded;
                    else if (score < veryHardMin)
                        category = TierListCategory.Overrated;
                    else if (score < hardMin)
                        category = TierListCategory.VeryEasy;
                    else if (score < mediumMin)
                        category = TierListCategory.Easy;
                    else if (score < easyMin)
                        category = TierListCategory.Medium;
                    else if (score < veryEasyMin)
                        category = TierListCategory.Hard;
                    else if (score < oneLevelOverrated)
                        category = TierListCategory.VeryHard;
                    else
                        category = TierListCategory.Underrated;

                    await _tierLists.SaveEntry(new SongTierListEntry("Popularity", chart.Id, category, score),
                        cancellationToken);
                }
            }

            return Unit.Value;
        }

        public async Task<Unit> Handle(ImportOfficialPlayerScoresCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            var maxPages = await _officialSite.GetScorePageCount(request.Username, request.Password, cancellationToken);
            var limit = (await _user.GetUserUiSettings(userId, cancellationToken)).TryGetValue("PreviousPageCount",
                out var result)
                ? int.TryParse(result, out var previous) ? (int?)maxPages - previous + 1 : null
                : null;

            var scores =
                (await _officialSite.GetRecordedScores(request.Username, request.Password, limit, cancellationToken))
                .ToArray();
            var count = 0;
            var batch = new List<RecordedPhoenixScore>();
            foreach (var score in scores)
            {
                await _mediator.Send(
                    new UpdatePhoenixBestAttemptCommand(score.Chart.Id, false, score.Score, score.Plate, true),
                    cancellationToken);
                count++;
                batch.Add(new RecordedPhoenixScore(score.Chart.Id, score.Score, score.Plate, false,
                    DateTimeOffset.Now));

                if (count % 10 != 0) continue;

                await _bus.Publish(new PlayerScoreUpdatedEvent(_currentUser.User.Id), cancellationToken);
                await _mediator.Publish(
                    new ImportStatusUpdated(_currentUser.User.Id,
                        $"Saving chart result {count} of {scores.Length}",
                        batch.ToArray()),
                    cancellationToken);
                batch.Clear();
            }

            await _mediator.Publish(
                new ImportStatusUpdated(_currentUser.User.Id,
                    "Charts finished saving",
                    batch.ToArray()),
                cancellationToken);
            batch.Clear();

            var settings = await _user.GetUserUiSettings(userId, cancellationToken);
            settings["PreviousPageCount"] = maxPages.ToString();
            await _user.SaveUserUiSettings(userId, settings, cancellationToken);

            return Unit.Value;
        }
    }
}
