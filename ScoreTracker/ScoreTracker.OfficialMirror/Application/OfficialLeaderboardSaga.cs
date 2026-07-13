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
        IRequestHandler<ExecuteImportCommand>,
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
            return await _worldRankings.GetTop50(request.Mix, request.Username, request.Type, cancellationToken);
        }

        public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetWorldRankingScoresQuery request,
            CancellationToken cancellationToken)
        {
            return await _worldRankings.GetAll(request.Mix, request.Username, cancellationToken);
        }

        public async Task<IEnumerable<(string Username, Uri AvatarPath)>> Handle(GetUserAvatarsQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetUserAvatars(cancellationToken);
        }

        public async Task<IEnumerable<WorldRankingRecord>> Handle(GetAllWorldRankingsQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetAllWorldRankings(request.Mix, cancellationToken);
        }

        public async Task<IEnumerable<string>> Handle(GetOfficialLeaderboardUsernamesQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetOfficialLeaderboardUsernames(request.Mix, cancellationToken);
        }

        public async Task<IEnumerable<UserOfficialLeaderboard>> Handle(GetOfficialLeaderboardStatusesQuery request,
            CancellationToken cancellationToken)
        {
            return await _leaderboards.GetOfficialLeaderboardStatuses(request.Mix, request.Username,
                cancellationToken);
        }

        public async Task<PiuGameUcsEntry?> Handle(GetOfficialUcsEntryQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetUcs(request.PiuGameId, cancellationToken);
        }

        public async Task<PiuGameAccountDataImport> Handle(GetOfficialAccountDataQuery request,
            CancellationToken cancellationToken)
        {
            var sid = await _officialSite.SignIn(request.Mix, request.Username, request.Password, cancellationToken);
            return await _officialSite.GetAccountData(request.Mix, sid, null, cancellationToken);
        }

        public async Task<Contracts.PiuGameAccountIdentity> Handle(GetPiuGameAccountIdentityQuery request,
            CancellationToken cancellationToken)
        {
            return await _officialSite.GetAccountIdentity(request.Mix, request.Username, request.Password,
                cancellationToken);
        }

        public async Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> Handle(
            GetOfficialRecentScoresQuery request, CancellationToken cancellationToken)
        {
            return await _officialSite.GetRecentScores(request.Mix, request.Username, request.Password,
                cancellationToken);
        }

        public async Task Handle(ProcessOfficialLeaderboardsCommand request, CancellationToken cancellationToken)
        {
            var leaderboardEntries =
                (await _officialSite.GetLeaderboardEntries(request.Mix, cancellationToken)).ToArray();
            foreach (var leaderboard in leaderboardEntries.GroupBy(l => l.LeaderboardName))
            {
                await _leaderboards.ClearLeaderboard(request.Mix, "Rating", leaderboard.Key, cancellationToken);
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
                await _leaderboards.WriteEntries(request.Mix, batch, cancellationToken);
            }

            var scores = (await _officialSite.GetAllOfficialChartScores(request.Mix, CancellationToken.None))
                .ToArray();
            foreach (var group in scores.GroupBy(u => u.Username))
                await _leaderboards.SaveAvatar(group.Key, group.First().AvatarUrl, cancellationToken);
            await PopulateTierLists(request.Mix, scores, cancellationToken);
            await SaveUserLeaderboards(request.Mix, scores, cancellationToken);
        }

        private async Task SaveUserLeaderboards(MixEnum mix, IEnumerable<OfficialChartLeaderboardEntry> entries,
            CancellationToken cancellationToken)
        {
            foreach (var group in entries.GroupBy(e => e.Chart.Id))
            {
                var leaderboardName = group.First().Chart.Song.Name + " " + group.First().Chart.DifficultyString;
                await _leaderboards.ClearLeaderboard(mix, "Chart", leaderboardName, cancellationToken);
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
                await _leaderboards.WriteEntries(mix, batch, cancellationToken);
            }
        }

        private async Task PopulateTierLists(MixEnum mix, IEnumerable<OfficialChartLeaderboardEntry> entries,
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
                foreach (var entry in tierListEntries)
                    await _tierLists.SaveEntry(mix, entry, cancellationToken);
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
            var entries = await _officialSite.GetOfficialChartLeaderboardEntries(request.Mix, cancellationToken);
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

                    await _tierLists.SaveEntry(request.Mix,
                        new SongTierListEntry("Popularity", chart.Id, category, score),
                        cancellationToken);
                }
            }
        }

        public async Task Handle(ImportOfficialPlayerScoresCommand request, CancellationToken cancellationToken)
        {
            var sid = await _officialSite.SignIn(request.Mix, request.Username, request.Password, cancellationToken);
            await RunImport(_currentUser.User.Id, request.Mix, sid, request.Id, request.ExpectedGameTag,
                request.IncludeBroken, request.SyncPiuTracker, cancellationToken);
        }

        public Task Handle(ExecuteImportCommand request, CancellationToken cancellationToken)
        {
            return RunImport(request.UserId, request.Mix, request.Sid, request.CardId, request.ExpectedGameTag,
                request.IncludeBroken, request.SyncPiuTracker, cancellationToken);
        }

        // Runs the scrape+save for one import off a pre-minted session id and an explicit user id,
        // so the same body serves the synchronous API path and the background consumer (which has
        // no circuit user). One import = one session id for the Session Batcher.
        internal async Task RunImport(Guid userId, MixEnum mix, string sid, string cardId, string expectedGameTag,
            bool includeBroken, bool syncPiuTracker, CancellationToken cancellationToken)
        {
            var importSessionId = Guid.NewGuid();

            var accountData =
                await _officialSite.GetAccountData(mix, sid, cardId, cancellationToken);
            if (accountData.AccountName != expectedGameTag)
            {
            }

            if (accountData.AccountName == "INVALID")
                await _mediator.Publish(new ImportStatusUpdatedEvent(userId,
                    "Invalid Login Information", Array.Empty<RecordedPhoenixScore>(), mix),
                    cancellationToken);

            if (mix == MixEnum.Phoenix2)
                await BackfillCardAliases(userId, mix, sid, cancellationToken);

            if (syncPiuTracker)
            {
                await _mediator.Publish(new ImportStatusUpdatedEvent(userId,
                    "Syncing PIU Tracker... (Can take a while if it's your first time)",
                    Array.Empty<RecordedPhoenixScore>(), mix));
                try
                {
                    await _piuTracker.SyncData(accountData.AccountName, accountData.Sid, cancellationToken);
                }
                catch (PiuTrackerUsedTooRecentException)
                {
                    await _mediator.Publish(
                        new ImportStatusErrorEvent(userId,
                            "PIU Tracker sync failed, you've imported too recently.", mix),
                        cancellationToken);
                }
                catch (Exception e)
                {
                    await _mediator.Publish(
                        new ImportStatusErrorEvent(userId,
                            "PIU Tracker sync failed. Check with DrMurloc or Tusa if this persists",
                            mix),
                        cancellationToken);
                    _logger.LogWarning(e, "PIU Tracker sync failed");
                }
            }

            // A scrape that yielded no recognizable avatar keeps the player's existing
            // one — persisting the miss is what used to break avatars sporadically.
            if (accountData.AvatarUrl != null)
                await _mediator.Send(new SaveUserUiSettingCommand("ProfileImage", accountData.AvatarUrl.ToString()),
                    cancellationToken);
            await _mediator.Send(new SaveUserUiSettingCommand("GameTag", accountData.AccountName), cancellationToken);
            // User writes go through Identity contracts — the Mirror never touches
            // IUserRepository (ADR-001: writes are owned by their vertical).
            await _mediator.Send(
                new UpdateUserGameProfileCommand(accountData.AccountName, accountData.AvatarUrl),
                cancellationToken);

            var maxPages =
                await _officialSite.GetScorePageCount(mix, sid, cancellationToken);
            // Page-count memory is per mix — the parallel sites paginate independently, so a
            // Phoenix 2 import must not shrink (or inflate) the next Phoenix 1 delta read.
            // Phoenix keeps the legacy key so existing users' next import stays incremental.
            var pageCountSetting = mix == MixEnum.Phoenix
                ? "PreviousPageCount"
                : $"PreviousPageCount__{mix}";
            var limit = (await _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken)).TryGetValue(
                pageCountSetting,
                out var result)
                ? int.TryParse(result, out var previous) ? (int?)maxPages - previous + 1 : null
                : null;

            var scores =
                (await _officialSite.GetRecordedScores(mix, userId, sid, cardId, includeBroken, limit,
                    cancellationToken))
                .ToArray();
            var count = 0;
            var batch = new List<RecordedPhoenixScore>();
            var existingScores =
                (await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix), cancellationToken))
                .ToDictionary(s =>
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
                        Source: ScoreJournalEntry.OfficialImportSource, Mix: mix,
                        SessionId: importSessionId),
                    cancellationToken);
                count++;
                batch.Add(new RecordedPhoenixScore(score.Chart.Id, score.Score, score.Plate, score.IsBroken,
                    _dateTime.Now));

                if (count % 10 != 0) continue;

                await _mediator.Publish(
                    new ImportStatusUpdatedEvent(userId,
                        $"Saving chart result {count} of {scores.Length}",
                        batch.ToArray(), mix),
                    cancellationToken);
                batch.Clear();
            }

            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId,
                    "Charts finished saving",
                    batch.ToArray(), mix),
                cancellationToken);
            batch.Clear();

            // Titles are announced last, now that we know whether this run saved any scores.
            // With a score batch, they ride its session snapshot card (SessionId flows to the
            // title path); with none, SessionId stays null and they get their own announcement.
            await _bus.Publish(new TitlesDetectedEvent(userId, accountData.Titles.Select(t => t.ToString()),
                    mix, toSave.Length > 0 ? importSessionId : null),
                cancellationToken);

            await _mediator.Send(new SaveUserUiSettingCommand(pageCountSetting, maxPages.ToString()),
                cancellationToken);
        }

        /// <summary>
        ///     /Login/PiuGame stays pinned to Phoenix 1 as the identity source (locked
        ///     decision), so card:{id} aliases from the Phoenix 2 site can only enter through
        ///     a P2 import. Additively attach any unclaimed ones to the importing account —
        ///     mirroring ResolveExternalUserCommand's backfill: aliases owned by a different
        ///     account are never re-pointed, they stay where they are.
        /// </summary>
        private async Task BackfillCardAliases(Guid userId, MixEnum mix, string sid,
            CancellationToken cancellationToken)
        {
            var cards = await _officialSite.GetGameCards(mix, sid, cancellationToken);
            foreach (var alias in cards.Select(c => $"card:{c.Id}"))
            {
                var owner = await _mediator.Send(new GetUserByExternalLoginQuery(alias, "PiuGame"),
                    cancellationToken);
                if (owner == null)
                    await _mediator.Send(new CreateExternalLoginCommand(userId, alias, "PiuGame"),
                        cancellationToken);
            }
        }

        private static readonly Regex NonAlphanumeric = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

        public async Task Handle(UpdateSongImagesCommand request, CancellationToken cancellationToken)
        {
            // Song images are shared per song, not per mix — sourced from the Phoenix 1 site
            // until the Phoenix 2 new-content admin workflow lands (post-release track).
            var entries =
                await _officialSite.GetOfficialChartLeaderboardEntries(MixEnum.Phoenix, cancellationToken);
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
            var sid = await _officialSite.SignIn(request.Mix, request.Username, request.Password, cancellationToken);
            return await _officialSite.GetGameCards(request.Mix, sid, cancellationToken);
        }

        public Task<DateTimeOffset?> Handle(GetLastLeaderboardImportTimestampQuery request,
            CancellationToken cancellationToken)
        {
            return _leaderboards.GetLastImportTimestamp(request.Mix, cancellationToken);
        }

        public async Task Consume(ConsumeContext<StartLeaderboardImportCommand> context)
        {
            var mix = context.Message.Mix;
            await _mediator.Send(new ProcessChartPopularityCommand(mix));
            await _mediator.Send(new ProcessOfficialLeaderboardsCommand(mix));
            await _worldRankings.CalculateWorldRankings(mix, CancellationToken.None);
            await _leaderboards.SetLastImportTimestamp(mix, _dateTime.Now, context.CancellationToken);
        }
    }
}
