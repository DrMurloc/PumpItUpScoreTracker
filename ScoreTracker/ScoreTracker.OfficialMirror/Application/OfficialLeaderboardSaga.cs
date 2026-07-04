using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Domain;
using System.Text.RegularExpressions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Application
{
    internal sealed class OfficialLeaderboardSaga : IRequestHandler<ProcessOfficialLeaderboardsCommand>,
        IRequestHandler<ProcessChartPopularityCommand>,
        IRequestHandler<ImportOfficialPlayerScoresCommand>,
        IRequestHandler<UpdateSongImagesCommand>,
        IRequestHandler<GetGameCardsQuery, IEnumerable<GameCardRecord>>,
        IRequestHandler<GetLastLeaderboardImportTimestampQuery, DateTimeOffset?>,
        IRequestHandler<GetWorldRankingTop50Query, IEnumerable<RecordedPhoenixScore>>,
        IRequestHandler<GetWorldRankingScoresQuery, IEnumerable<RecordedPhoenixScore>>,
        IRequestHandler<GetUserAvatarsQuery, IEnumerable<(string Username, Uri AvatarPath)>>,
        IRequestHandler<GetAllWorldRankingsQuery, IEnumerable<WorldRankingRecord>>,
        IRequestHandler<GetOfficialLeaderboardUsernamesQuery, IEnumerable<string>>,
        IRequestHandler<GetOfficialLeaderboardStatusesQuery, IEnumerable<UserOfficialLeaderboard>>,
        IRequestHandler<GetOfficialUcsEntryQuery, PiuGameUcsEntry?>,
        IRequestHandler<GetOfficialAccountDataQuery, PiuGameAccountDataImport>,
        IRequestHandler<GetPiuGameAccountIdentityQuery, Contracts.PiuGameAccountIdentity>,
        IRequestHandler<GetOfficialRecentScoresQuery, (IEnumerable<OfficialRecordedScore> results,
            IEnumerable<string> nonMapped)>,
        IConsumer<StartLeaderboardImportCommand>
    {
        private readonly IOfficialSiteClient _officialSite;
        private readonly ITierListRepository _tierLists;
        private readonly IOfficialLeaderboardRepository _leaderboards;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IWorldRankingService _worldRankings;
        private readonly IMediator _mediator;
        private readonly IBus _bus;
        private readonly IFileUploadClient _files;
        private readonly IChartRepository _charts;
        private readonly IPiuTrackerClient _piuTracker;
        private readonly ILogger _logger;
        private readonly IDateTimeOffsetAccessor _dateTime;

        public OfficialLeaderboardSaga(IOfficialSiteClient officialSite, ITierListRepository tierLists,
            IWorldRankingService worldRankings,
            IOfficialLeaderboardRepository leaderboards, ICurrentUserAccessor currentUser,
            IMediator mediator,
            IPiuTrackerClient piuTracker,
            ILogger<OfficialLeaderboardSaga> logger,
            IBus bus, IFileUploadClient files, IChartRepository charts,
            IDateTimeOffsetAccessor dateTime)
        {
            _piuTracker = piuTracker;
            _officialSite = officialSite;
            _tierLists = tierLists;
            _leaderboards = leaderboards;
            _currentUser = currentUser;
            _mediator = mediator;
            _logger = logger;
            _bus = bus;
            _files = files;
            _charts = charts;
            _worldRankings = worldRankings;
            _dateTime = dateTime;
        }

        // Read-side pass-throughs for the OfficialLeaderboards/Competition pages (rearch C39):
        // pages dispatch via IMediator so these ports can go Mirror-internal at the extraction.
        public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetWorldRankingTop50Query request,
            CancellationToken cancellationToken)
        {
            return await _worldRankings.GetTop50(request.Username, request.Type, cancellationToken);
        }

        public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetWorldRankingScoresQuery request,
            CancellationToken cancellationToken)
        {
            return await _worldRankings.GetAll(request.Username, cancellationToken);
        }

        public async Task<IEnumerable<(string Username, Uri AvatarPath)>> Handle(GetUserAvatarsQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetUserAvatars(cancellationToken);
        }

        public async Task<IEnumerable<WorldRankingRecord>> Handle(GetAllWorldRankingsQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetAllWorldRankings(cancellationToken);
        }

        public async Task<IEnumerable<string>> Handle(GetOfficialLeaderboardUsernamesQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetOfficialLeaderboardUsernames(cancellationToken);
        }

        public async Task<IEnumerable<UserOfficialLeaderboard>> Handle(GetOfficialLeaderboardStatusesQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetOfficialLeaderboardStatuses(request.Username, cancellationToken);
        }

        public async Task<PiuGameUcsEntry?> Handle(GetOfficialUcsEntryQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetUcs(request.PiuGameId, cancellationToken);
        }

        public async Task<PiuGameAccountDataImport> Handle(GetOfficialAccountDataQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetAccountData(request.Username, request.Password, null, cancellationToken);
        }

        public async Task<Contracts.PiuGameAccountIdentity> Handle(GetPiuGameAccountIdentityQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetAccountIdentity(request.Username, request.Password, cancellationToken);
        }

        public async Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> Handle(
            GetOfficialRecentScoresQuery request, CancellationToken cancellationToken)
        {
            return await _officialSite.GetRecentScores(request.Username, request.Password, cancellationToken);
        }

        public async Task Handle(ProcessOfficialLeaderboardsCommand request, CancellationToken cancellationToken)
        {
            var leaderboardEntries = (await _officialSite.GetLeaderboardEntries(cancellationToken)).ToArray();
            foreach (var leaderboard in leaderboardEntries.GroupBy(l => l.LeaderboardName))
            {
                await _leaderboards.ClearLeaderboard("Rating", leaderboard.Key, cancellationToken);
                var batch = new List<UserOfficialLeaderboard>();
                var place = 1;
                foreach (var scoreGroup in leaderboard.GroupBy(l => l.Score).OrderByDescending(kv => kv.Key))
                {
                    var currentPlace = place;
                    foreach (var entry in scoreGroup)
                    {
                        batch.Add(entry with { Place = currentPlace });
                        place++;
                    }
                }
                await _leaderboards.WriteEntries(batch, cancellationToken);
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
                var batch = new List<UserOfficialLeaderboard>();
                var place = 1;
                foreach (var scoreGroup in group.GroupBy(e => (int)e.Score).OrderByDescending(g => g.Key))
                {
                    var currentPlace = place;
                    foreach (var entry in scoreGroup)
                    {
                        batch.Add(new UserOfficialLeaderboard(entry.Username, currentPlace, "Chart", leaderboardName,
                            entry.Score));
                        place++;
                    }
                }
                await _leaderboards.WriteEntries(batch, cancellationToken);
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
                var tierListEntries = TierListProcessor.ProcessIntoTierList(group.Value, group.Key.Level, "Official Scores");
                // Phoenix until per-mix computation lands (plan doc, saga commit) — the
                // official mirror scrapes the Phoenix site only until the import commit.
                foreach (var entry in tierListEntries)
                    await _tierLists.SaveEntry(MixEnum.Phoenix, entry, cancellationToken);
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

                    // Phoenix until per-mix computation lands (plan doc, saga commit) — the
                    // official mirror scrapes the Phoenix site only until the import commit.
                    await _tierLists.SaveEntry(MixEnum.Phoenix,
                        new SongTierListEntry("Popularity", chart.Id, category, score),
                        cancellationToken);
                }
            }
        }

        public async Task Handle(ImportOfficialPlayerScoresCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;

            var accountData =
                await _officialSite.GetAccountData(request.Username, request.Password, request.Id, cancellationToken);
            if (accountData.AccountName != request.ExpectedGameTag)
            {
            }

            // Phoenix until per-mix computation lands (plan doc, saga commit).
            if (accountData.AccountName == "INVALID")
                await _mediator.Publish(new ImportStatusUpdatedEvent(_currentUser.User.Id,
                    "Invalid Login Information", Array.Empty<RecordedPhoenixScore>(), MixEnum.Phoenix),
                    cancellationToken);


            if (request.SyncPiuTracker)
            {
                await _mediator.Publish(new ImportStatusUpdatedEvent(_currentUser.User.Id,
                    "Syncing PIU Tracker... (Can take a while if it's your first time)",
                    Array.Empty<RecordedPhoenixScore>(), MixEnum.Phoenix));
                try
                {
                    await _piuTracker.SyncData(accountData.AccountName, accountData.Sid, cancellationToken);
                }
                catch (PiuTrackerUsedTooRecentException)
                {
                    await _mediator.Publish(
                        new ImportStatusErrorEvent(userId,
                            "PIU Tracker sync failed, you've imported too recently.", MixEnum.Phoenix),
                        cancellationToken);
                }
                catch (Exception e)
                {
                    await _mediator.Publish(
                        new ImportStatusErrorEvent(userId,
                            "PIU Tracker sync failed. Check with DrMurloc or Tusa if this persists",
                            MixEnum.Phoenix),
                        cancellationToken);
                    _logger.LogWarning(e, "PIU Tracker sync failed");
                }
            }

            await _mediator.Send(new SaveUserUiSettingCommand("ProfileImage", accountData.AvatarUrl.ToString()),
                cancellationToken);
            await _mediator.Send(new SaveUserUiSettingCommand("GameTag", accountData.AccountName), cancellationToken);
            // User writes go through Identity contracts — the Mirror never touches
            // IUserRepository (ADR-001: writes are owned by their vertical).
            await _mediator.Send(
                new UpdateUserGameProfileCommand(accountData.AccountName, accountData.AvatarUrl),
                cancellationToken);

            // Phoenix until per-mix computation lands (plan doc, saga commit).
            await _bus.Publish(new TitlesDetectedEvent(userId, accountData.Titles.Select(t => t.ToString()),
                    MixEnum.Phoenix),
                cancellationToken);

            var maxPages = await _officialSite.GetScorePageCount(request.Username, request.Password, cancellationToken);
            var limit = (await _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken)).TryGetValue("PreviousPageCount",
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
                    new UpdatePhoenixBestAttemptCommand(score.Chart.Id, score.IsBroken, score.Score, score.Plate,
                        Source: ScoreJournalEntry.OfficialImportSource),
                    cancellationToken);
                count++;
                batch.Add(new RecordedPhoenixScore(score.Chart.Id, score.Score, score.Plate, score.IsBroken,
                    _dateTime.Now));

                if (count % 10 != 0) continue;

                await _mediator.Publish(
                    new ImportStatusUpdatedEvent(_currentUser.User.Id,
                        $"Saving chart result {count} of {scores.Length}",
                        batch.ToArray(), MixEnum.Phoenix),
                    cancellationToken);
                batch.Clear();
            }

            await _mediator.Publish(
                new ImportStatusUpdatedEvent(_currentUser.User.Id,
                    "Charts finished saving",
                    batch.ToArray(), MixEnum.Phoenix),
                cancellationToken);
            batch.Clear();

            await _mediator.Send(new SaveUserUiSettingCommand("PreviousPageCount", maxPages.ToString()),
                cancellationToken);
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

        public Task<DateTimeOffset?> Handle(GetLastLeaderboardImportTimestampQuery request,
            CancellationToken cancellationToken)
        {
            return _leaderboards.GetLastImportTimestamp(cancellationToken);
        }

        public async Task Consume(ConsumeContext<StartLeaderboardImportCommand> context)
        {
            await _mediator.Send(new ProcessChartPopularityCommand());
            await _mediator.Send(new ProcessOfficialLeaderboardsCommand());
            await _worldRankings.CalculateWorldRankings(CancellationToken.None);
            await _leaderboards.SetLastImportTimestamp(_dateTime.Now, context.CancellationToken);
        }
    }
}
