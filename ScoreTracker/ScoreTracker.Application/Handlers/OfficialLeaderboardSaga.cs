using System.Text.RegularExpressions;
using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class OfficialLeaderboardSaga : IRequestHandler<ProcessOfficialLeaderboardsCommand>,
        IRequestHandler<ProcessChartPopularityCommand>,
        IRequestHandler<ImportOfficialPlayerScoresCommand>,
        IRequestHandler<UpdateSongImagesCommand>,
        IRequestHandler<GetGameCardsQuery, IEnumerable<GameCardRecord>>
    {
        private readonly IOfficialSiteClient _officialSite;
        private readonly ITierListRepository _tierLists;
        private readonly IOfficialLeaderboardRepository _leaderboards;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IUserRepository _user;
        private readonly IMediator _mediator;
        private readonly IBus _bus;
        private readonly IFileUploadClient _files;
        private readonly IChartRepository _charts;

        public OfficialLeaderboardSaga(IOfficialSiteClient officialSite, ITierListRepository tierLists,
            IOfficialLeaderboardRepository leaderboards, ICurrentUserAccessor currentUser, IUserRepository user,
            IMediator mediator,
            IBus bus, IFileUploadClient files, IChartRepository charts)
        {
            _officialSite = officialSite;
            _tierLists = tierLists;
            _leaderboards = leaderboards;
            _currentUser = currentUser;
            _user = user;
            _mediator = mediator;
            _bus = bus;
            _files = files;
            _charts = charts;
        }

        public async Task Handle(ProcessOfficialLeaderboardsCommand request, CancellationToken cancellationToken)
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
            foreach (var group in scores.GroupBy(u => u.Username))
                await _leaderboards.SaveAvatar(group.Key, group.First().AvatarUrl, cancellationToken);
            await PopulateTierLists(scores, cancellationToken);
            await SaveUserLeaderboards(scores, cancellationToken);
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
            var chartLevelGroups = entries.GroupBy(e => (e.Chart.Type, e.Chart.Level))
                .ToDictionary(g => g.Key,
                    g => g.GroupBy(e => e.Username)
                        .ToDictionary(gu => gu.Key,
                            gu => (IDictionary<Guid, PhoenixScore>)gu.ToDictionary(u => u.Chart.Id, u => u.Score)));

            foreach (var group in chartLevelGroups)
            {
                var tierListEntries = TierListSaga.ProcessIntoTierList(group.Value, group.Key.Level, "Official Scores");
                foreach (var entry in tierListEntries) await _tierLists.SaveEntry(entry, cancellationToken);
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

        public async Task Handle(ProcessChartPopularityCommand request, CancellationToken cancellationToken)
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
                foreach (var (chart, score, url) in levelTypeGroup)
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
        }

        public async Task Handle(ImportOfficialPlayerScoresCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;

            var accountData = await _officialSite.GetAccountData(request.Username, request.Password, cancellationToken);
            if (accountData.AccountName == "INVALID")
                await _mediator.Publish(new ImportStatusUpdated(_currentUser.User.Id,
                    "Invalid Login Information", Array.Empty<RecordedPhoenixScore>()), cancellationToken);

            await _mediator.Send(new SaveUserUiSettingCommand("ProfileImage", accountData.AvatarUrl.ToString()),
                cancellationToken);
            await _mediator.Send(new SaveUserUiSettingCommand("GameTag", accountData.AccountName), cancellationToken);
            var user = await _user.GetUser(userId, cancellationToken);
            await _user.SaveUser(
                new User(userId, user.Name, user.IsPublic, accountData.AccountName, accountData.AvatarUrl,
                    user.Country),
                cancellationToken);

            await _bus.Publish(new TitlesDetectedEvent(user.Id, accountData.Titles.Select(t => t.ToString())),
                cancellationToken);

            var maxPages = await _officialSite.GetScorePageCount(request.Username, request.Password, cancellationToken);
            var limit = (await _user.GetUserUiSettings(userId, cancellationToken)).TryGetValue("PreviousPageCount",
                out var result)
                ? int.TryParse(result, out var previous) ? (int?)maxPages - previous + 1 : null
                : null;

            var scores =
                (await _officialSite.GetRecordedScores(_currentUser.User.Id, request.Username, request.Password,
                    request.Id,
                    request.IncludeBroken, limit,
                    cancellationToken))
                .ToArray();
            var count = 0;
            var batch = new List<RecordedPhoenixScore>();
            var existingScores =
                (await _mediator.Send(new GetPhoenixRecordsQuery(userId), cancellationToken)).ToDictionary(s =>
                    s.ChartId);
            var toSave = scores.Where(s =>
                    !existingScores.TryGetValue(s.Chart.Id, out var sc) || (sc.IsBroken && !s.IsBroken) ||
                    (sc.IsBroken == s.IsBroken && (sc.Plate < s.Plate ||
                                                   sc.Score < s.Score)))
                .ToArray();
            foreach (var score in toSave)
            {
                await _mediator.Send(
                    new UpdatePhoenixBestAttemptCommand(score.Chart.Id, score.IsBroken, score.Score, score.Plate),
                    cancellationToken);
                count++;
                batch.Add(new RecordedPhoenixScore(score.Chart.Id, score.Score, score.Plate, score.IsBroken,
                    DateTimeOffset.Now));

                if (count % 10 != 0) continue;

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
        }

        private static readonly Regex NonAlphanumeric = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

        public async Task Handle(UpdateSongImagesCommand request, CancellationToken cancellationToken)
        {
            var entries = await _officialSite.GetOfficialChartLeaderboardEntries(cancellationToken);
            foreach (var songGroup in entries.GroupBy(e => e.Chart.Song.Name))
            {
                var song = songGroup.First().Chart.Song;
                var songHasImageAlready = !song.ImagePath.ToString()
                    .EndsWith("placeholder.png", StringComparison.OrdinalIgnoreCase);
                if (!request.IncludeSongsAlreadyWithImages &&
                    songHasImageAlready) continue;

                var piuGamePath = songGroup.First().SongImage;
                var newImage = songHasImageAlready
                    ? song.ImagePath.PathAndQuery
                    : "/songs/" + NonAlphanumeric.Replace(song.Name, "") + "." +
                      piuGamePath.GetLeftPart(UriPartial.Path).Split(".")[^1];
                var newPath = await _files.CopyFromSource(piuGamePath, newImage, cancellationToken);
                await _charts.UpdateSongImage(song.Name, newPath, cancellationToken);
            }
        }

        public async Task<IEnumerable<GameCardRecord>> Handle(GetGameCardsQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetGameCards(request.Username, request.Password, cancellationToken);
        }
    }
}
