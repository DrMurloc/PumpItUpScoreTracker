using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Domain;
using System.Text.RegularExpressions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.ScoreLedger.Contracts;
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
    internal sealed class OfficialLeaderboardSaga : IRequestHandler<ImportOfficialPlayerScoresCommand>,
        IRequestHandler<ExecuteImportCommand, int>,
        IRequestHandler<SaveOfficialScoresCommand, int>,
        IRequestHandler<UpdateSongImagesCommand>,
        IRequestHandler<GetGameCardsQuery, IEnumerable<GameCardRecord>>,
        IRequestHandler<GetOfficialUcsEntryQuery, PiuGameUcsEntry?>,
        IRequestHandler<GetOfficialAccountDataQuery, PiuGameAccountDataImport>,
        IRequestHandler<GetPiuGameAccountIdentityQuery, Contracts.PiuGameAccountIdentity>
    {
        private readonly IOfficialSiteClient _officialSite;
        private readonly IOfficialPlayerIdentityRepository _identity;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMediator _mediator;
        private readonly IBus _bus;
        private readonly IFileUploadClient _files;
        private readonly IChartRepository _charts;
        private readonly ISessionDeliveryClient _sessionDelivery;
        private readonly ILogger _logger;
        private readonly IDateTimeOffsetAccessor _dateTime;

        public OfficialLeaderboardSaga(IOfficialSiteClient officialSite,
            IOfficialPlayerIdentityRepository identity,
            ICurrentUserAccessor currentUser,
            IMediator mediator,
            ISessionDeliveryClient sessionDelivery,
            ILogger<OfficialLeaderboardSaga> logger,
            IBus bus, IFileUploadClient files, IChartRepository charts,
            IDateTimeOffsetAccessor dateTime)
        {
            _sessionDelivery = sessionDelivery;
            _officialSite = officialSite;
            _identity = identity;
            _currentUser = currentUser;
            _mediator = mediator;
            _logger = logger;
            _bus = bus;
            _files = files;
            _charts = charts;
            _dateTime = dateTime;
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

        public async Task Handle(ImportOfficialPlayerScoresCommand request, CancellationToken cancellationToken)
        {
            var sid = await _officialSite.SignIn(request.Mix, request.Username, request.Password, cancellationToken);
            _ = await RunImport(_currentUser.User.Id, request.Mix, sid, request.Id, request.ExpectedGameTag,
                request.IncludeBroken, cancellationToken);
        }

        public Task<int> Handle(ExecuteImportCommand request, CancellationToken cancellationToken)
        {
            return RunImport(request.UserId, request.Mix, request.Sid, request.CardId, request.ExpectedGameTag,
                request.IncludeBroken, cancellationToken, request.SessionId);
        }

        // Runs the scrape+save for one import off a pre-minted session id and an explicit user id,
        // so the same body serves the synchronous API path and the background consumer (which has
        // no circuit user). One import = one session id for the Session Batcher.
        internal async Task<int> RunImport(Guid userId, MixEnum mix, string sid, string cardId,
            string expectedGameTag, bool includeBroken, CancellationToken cancellationToken, Guid? sessionId = null)
        {
            // Opened through the Ledger rather than minted here, so the session row carries the
            // game tag and card this run pulled from — the answer to "I imported the wrong card",
            // which is the phrasing the Undo page exists for. A caller that already opened one
            // (the completeness check, which saves through two passes) hands it in, so a single
            // button press produces a single session.
            var importSessionId = sessionId ?? await _mediator.Send(
                new BeginScoreSessionCommand(userId, mix, ScoreJournalEntry.OfficialImportSource,
                    expectedGameTag, cardId), cancellationToken);

            // Announce the run right away so the nav-bar "importing" indicator lights up while the
            // scrape works, even for a small import that saves fewer than one progress batch.
            await _mediator.Publish(new ImportStatusUpdatedEvent(userId, "Importing your scores…",
                Array.Empty<RecordedPhoenixScore>(), mix), cancellationToken);

            var accountData =
                await _officialSite.GetAccountData(mix, sid, cardId, cancellationToken);
            if (accountData.AccountName != expectedGameTag)
            {
            }

            // A signed-in session that can't resolve to a game account (wrong card, no profile
            // yet) is terminal — surface it as an error and stop rather than scraping nothing
            // and reporting a hollow "complete".
            if (accountData.AccountName == "INVALID")
            {
                await _mediator.Publish(new ImportStatusErrorEvent(userId, "Invalid Login Information", mix),
                    cancellationToken);
                return 0;
            }

            // The import learns the account's game tag authoritatively — the strongest
            // possible tag-to-account signal, so it always wins (most recent import takes a
            // contested tag, per the same-tag policy).
            var linkedTag =
                await _identity.LinkPlayer(mix, accountData.AccountName, userId, _dateTime.Now, cancellationToken);
            // Announced with the STORED spelling, not the one the account page rendered: a
            // consumer matching on the tag has to match the row this just wrote.
            await _bus.Publish(new OfficialPlayerLinkedEvent(mix, linkedTag, userId), cancellationToken);

            if (mix == MixEnum.Phoenix2)
                await BackfillCardAliases(userId, mix, sid, cancellationToken);

            // Hand the session to whichever tools this player granted it to — PIU Tracker is one of
            // them now rather than a hardcoded branch with its own checkbox. Whether it fires is a
            // share, not a per-import flag, and a tool's failure is the maker's problem: it lands in
            // their console, never in the player's import status.
            //
            // The port promises not to throw and the catch is here anyway. This runs before the
            // scrape, so anything escaping would take the player's whole import with it — too
            // expensive to stake on an implementation keeping a promise.
            try
            {
                await _sessionDelivery.DeliverSession(userId, mix, RedactedString.From(accountData.Sid),
                    accountData.AccountName, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Session delivery threw during import for {UserId}", userId);
            }

            await _mediator.Send(new SaveUserUiSettingCommand("GameTag", accountData.AccountName), cancellationToken);
            // User writes go through Identity contracts — the Mirror never touches
            // IUserRepository (ADR-001: writes are owned by their vertical).
            //
            // The avatar is stored in two places, and BOTH of them are this command's job now:
            // User.ProfileImage for the claims, and the ProfileImage UI setting for the shell's
            // app-bar avatar. The Mirror used to write that setting itself, right here, which
            // meant any rule about whose avatar wins had to be known in two verticals. It is not
            // the Mirror's rule to know (docs/design/avatar-selection.md §2).
            await _mediator.Send(
                new UpdateUserGameProfileCommand(accountData.AccountName, accountData.AvatarUrl),
                cancellationToken);

            var settings = await _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken);
            // Two incremental strategies: the classic (undated) best page imports by
            // page-count delta — Phoenix keeps its legacy key so existing users' next import
            // stays incremental — while dated pages import by saved-date watermark, keyed per
            // card because two cards on one account have independent score histories.
            int? maxPages = null;
            int? limit = null;
            const string pageCountSetting = "PreviousPageCount";
            if (mix == MixEnum.Phoenix)
            {
                maxPages = await _officialSite.GetScorePageCount(mix, sid, cancellationToken);
                limit = settings.TryGetValue(pageCountSetting, out var result)
                    ? int.TryParse(result, out var previous) ? maxPages - previous + 1 : null
                    : null;
            }

            var scrape = await _officialSite.GetRecordedScores(mix, userId, sid, cardId, includeBroken, limit,
                cancellationToken);
            var scores = scrape.Bests.ToArray();
            // The plays that never became a record are history too — journaled before the
            // bests, so a play arriving through both paths is one row that the best raises to
            // IsBest rather than a second row racing it.
            await _mediator.Send(new RecordObservedPlaysCommand(userId, mix,
                ScoreJournalEntry.OfficialImportSource, importSessionId, scrape.Plays), cancellationToken);
            var toSave = await SaveBests(userId, mix, importSessionId, scores, cancellationToken);
            // Titles are announced last, now that we know whether this run saved any scores.
            // With a score batch, they ride its session snapshot card (SessionId flows to the
            // title path); with none, SessionId stays null and they get their own announcement.
            await _bus.Publish(new TitlesDetectedEvent(userId, accountData.Titles.Select(t => t.ToString()),
                    mix, toSave.Length > 0 ? importSessionId : null),
                cancellationToken);

            if (maxPages != null)
                await _mediator.Send(new SaveUserUiSettingCommand(pageCountSetting, maxPages.Value.ToString()),
                    cancellationToken);

            return toSave.Length;
        }

        /// <summary>
        ///     Saves whatever a scrape found that beats what we already hold, announcing progress
        ///     as it goes. Shared by the import and by the completeness check's repair, so both
        ///     obey the same rule: a scrape may only ever RAISE a record, and only by the one
        ///     published policy — a second hand-written copy of that rule is what let plate
        ///     improvements drag scores down (docs/design/score-truth-model.md).
        /// </summary>
        internal async Task<OfficialRecordedScore[]> SaveBests(Guid userId, MixEnum mix, Guid sessionId,
            OfficialRecordedScore[] scores, CancellationToken cancellationToken)
        {
            var existingScores =
                (await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix), cancellationToken))
                .ToDictionary(s => s.ChartId);
            var toSave = scores.Where(s =>
                    BestAttemptPolicy.Beats(existingScores.GetValueOrDefault(s.Chart.Id), s.Score,
                        BestAttemptPolicy.PlateFor(s.IsBroken, s.Plate), s.IsBroken))
                .ToArray();

            var count = 0;
            var batch = new List<RecordedPhoenixScore>();
            foreach (var score in toSave)
            {
                await _mediator.Send(
                    new UpdatePhoenixBestAttemptCommand(score.Chart.Id, score.IsBroken, score.Score, score.Plate,
                        KeepBestStats: true,
                        Source: ScoreJournalEntry.OfficialImportSource, Mix: mix,
                        SessionId: sessionId,
                        RecordedAt: score.RecordedAt,
                        Judgements: score.Judgements),
                    cancellationToken);
                count++;
                batch.Add(new RecordedPhoenixScore(score.Chart.Id, score.Score, score.Plate, score.IsBroken,
                    score.RecordedAt ?? _dateTime.Now));

                if (count % 10 != 0) continue;

                await _mediator.Publish(
                    new ImportStatusUpdatedEvent(userId, $"Saving chart result {count} of {toSave.Length}",
                        batch.ToArray(), mix),
                    cancellationToken);
                batch.Clear();
            }

            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId, "Charts finished saving", batch.ToArray(), mix),
                cancellationToken);
            return toSave;
        }

        /// <summary>
        ///     Puts scores the completeness check scraped through the import's own save path, into
        ///     the session it opened — so a recovered chart obeys the same raise-only rule and Undo,
        ///     the journal and the rating sweep see it as part of one official run.
        /// </summary>
        public async Task<int> Handle(SaveOfficialScoresCommand request, CancellationToken cancellationToken)
        {
            var saved = await SaveBests(request.UserId, request.Mix, request.SessionId,
                request.Scores.ToArray(), cancellationToken);
            return saved.Length;
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
            var (entries, _) =
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

    }
}
