using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts;
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
    internal sealed class OfficialLeaderboardSaga :
        IRequestHandler<ImportOfficialPlayerScoresCommand, OfficialImportResult>,
        IRequestHandler<ExecuteImportCommand, int>,
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
        private readonly IImportConcurrencyGuard _guard;

        public OfficialLeaderboardSaga(IOfficialSiteClient officialSite,
            IOfficialPlayerIdentityRepository identity,
            ICurrentUserAccessor currentUser,
            IMediator mediator,
            ISessionDeliveryClient sessionDelivery,
            ILogger<OfficialLeaderboardSaga> logger,
            IBus bus, IFileUploadClient files, IChartRepository charts,
            IDateTimeOffsetAccessor dateTime, IImportConcurrencyGuard guard)
        {
            _guard = guard;
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

        public async Task<OfficialImportResult> Handle(ImportOfficialPlayerScoresCommand request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.User.Id;
            // Before the sign-in, so a call refused here costs piugame nothing — the same slot and cooldown
            // as the Import button, so a script cannot do what the button no longer can, and the save lock
            // sees this import too.
            var slot = _guard.TryBegin(userId, request.Mix, _dateTime.Now, cooldownApplies: true);
            if (slot.Outcome == ImportSlotOutcome.AlreadyRunning)
                return new OfficialImportResult(OfficialImportOutcome.AlreadyRunning);
            if (slot.Outcome == ImportSlotOutcome.CoolingDown)
                return new OfficialImportResult(OfficialImportOutcome.CoolingDown, slot.RetryAfter);

            try
            {
                var sid = await _officialSite.SignIn(request.Mix, request.Username, request.Password,
                    cancellationToken);
                // The card list comes off the same sign-in; an account with no card at all throws here, as
                // it always has.
                var cards = (await _officialSite.GetGameCards(request.Mix, sid, cancellationToken)).ToArray();
                var card = cards.First();
                if (!string.IsNullOrWhiteSpace(request.GameTag))
                {
                    var named = cards.FirstOrDefault(c => c.GameTag == request.GameTag);
                    if (named == null) return new OfficialImportResult(OfficialImportOutcome.GameTagNotFound);
                    card = named;
                }

                _guard.Started(userId, request.Mix, _dateTime.Now);
                await RunImport(userId, request.Mix, sid, card.Id, card.GameTag, request.IncludeBroken,
                    cancellationToken);
                return new OfficialImportResult(OfficialImportOutcome.Imported);
            }
            finally
            {
                _guard.End(userId);
            }
        }

        public Task<int> Handle(ExecuteImportCommand request, CancellationToken cancellationToken)
        {
            return RunImport(request.UserId, request.Mix, request.Sid, request.CardId, request.ExpectedGameTag,
                request.IncludeBroken, cancellationToken, request.SessionId, request.Kind);
        }

        /// <summary>
        ///     One import, at any of its three depths, off an explicit user id — so the same body serves
        ///     the synchronous API path and the background consumer (which has no circuit user). Every
        ///     depth imports first: counting an account that played twenty minutes ago against scores we
        ///     have not fetched yet reports charts that are simply not imported yet. A
        ///     <see cref="ImportKind.Check" /> then counts every level and re-reads the ones piugame
        ///     disagrees on; a <see cref="ImportKind.DeepScan" /> re-reads every best-score page. Either
        ///     way it is one session, one announcement and one "Charts finished saving" for the press
        ///     (docs/design/import-completeness-check.md). Returns how many records the run wrote.
        /// </summary>
        internal async Task<int> RunImport(Guid userId, MixEnum mix, string sid, string cardId,
            string expectedGameTag, bool includeBroken, CancellationToken cancellationToken, Guid? sessionId = null,
            ImportKind kind = ImportKind.Standard)
        {
            // Opened through the Ledger rather than minted here, so the session row carries the
            // game tag and card this run pulled from — the answer to "I imported the wrong card",
            // which is the phrasing the Undo page exists for. The background consumer opens it itself
            // and hands it in, so the run's ImportResult row can point at it before the scrape starts.
            var importSessionId = sessionId ?? await _mediator.Send(
                new BeginScoreSessionCommand(userId, mix, ScoreJournalEntry.OfficialImportSource,
                    expectedGameTag, cardId), cancellationToken);

            // Announce the run right away so the nav-bar "importing" indicator lights up while the
            // scrape works, even for a small import that saves fewer than one progress batch.
            await _mediator.Publish(new ImportStatusUpdatedEvent(userId, "Importing your scores…",
                Array.Empty<RecordedPhoenixScore>(), mix), cancellationToken);

            var accountData =
                await _officialSite.GetAccountData(mix, sid, cardId, cancellationToken);

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

            // What every save changed, kept by the run itself: nothing waits in the two-minute batch, so
            // this list is the only record of what the announcement has to say
            // (docs/design/import-restart-recovery.md §0).
            var saves = new List<ScoreSaveResult>();
            var titles = accountData.Titles.Select(t => t.ToString()).ToArray();
            int saved;
            var deeper = (Added: 0, Checked: 0);
            try
            {
                // A score typed in just before this run began opened a batch that would announce on its own
                // timer mid-run, beside this run's capture; claimed now, it is announced with the run, and
                // if the run fails it rides the announcement on the way out like any save.
                saves.AddRange(await _mediator.Send(new ClaimScoreBatchCommand(userId, mix), cancellationToken));
                var scrape = await _officialSite.GetRecordedScores(mix, userId, sid, cardId, includeBroken, limit,
                    cancellationToken);
                // The plays that never became a record are history too — journaled before the
                // bests, so a play arriving through both paths is one row that the best raises to
                // IsBest rather than a second row racing it.
                await _mediator.Send(new RecordObservedPlaysCommand(userId, mix,
                    ScoreJournalEntry.OfficialImportSource, importSessionId, scrape.Plays, includeBroken),
                    cancellationToken);
                saved = await SaveBests(userId, mix, importSessionId, scrape.Bests.ToArray(), saves,
                    cancellationToken);
                if (kind != ImportKind.Standard)
                    deeper = await ReadDeeper(userId, mix, sid, importSessionId, kind, includeBroken, saves,
                        cancellationToken);

                if (maxPages != null)
                    await _mediator.Send(new SaveUserUiSettingCommand(pageCountSetting, maxPages.Value.ToString()),
                        cancellationToken);
            }
            catch (Exception) when (saves.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                // What the run saved before it failed is real, and a later import will never bring it
                // back — those records already match — so it is announced now. Only here or below,
                // never both: nothing after the announcement can land in this catch. On shutdown the
                // token is cancelled and the startup pass replays the whole session instead.
                try
                {
                    await Announce(userId, mix, importSessionId, saves, titles, CancellationToken.None);
                }
                catch (Exception announceFailure)
                {
                    _logger.LogError(announceFailure,
                        "Could not announce the scores a failed import saved for {UserId} on {Mix}", userId, mix);
                }

                throw;
            }

            await Announce(userId, mix, importSessionId, saves, titles, cancellationToken);
            await _mediator.Publish(new ImportStatusUpdatedEvent(userId, "Charts finished saving",
                Array.Empty<RecordedPhoenixScore>(), mix), cancellationToken);
            if (kind != ImportKind.Standard)
                await _mediator.Publish(new ImportCheckCompletedEvent(userId, mix, deeper.Added, deeper.Checked),
                    cancellationToken);
            return saved + deeper.Added;
        }

        /// <summary>
        ///     The second pass of a check or a deep scan, into the same session and the same list of saves
        ///     as the import before it. A check counts every level and re-reads only the ones that
        ///     disagree, so a clean account pays for the census and nothing else. A deep scan skips the
        ///     count and re-reads every best-score page: the walk finds everything a count would have
        ///     pointed at, plus the one thing a count never could — a score improved without changing
        ///     grade or plate. Added is what this pass wrote; Checked is the census's pass total, or the
        ///     bests the walk read.
        /// </summary>
        private async Task<(int Added, int Checked)> ReadDeeper(Guid userId, MixEnum mix, string sid,
            Guid sessionId, ImportKind kind, bool includeBroken, ICollection<ScoreSaveResult> saves,
            CancellationToken cancellationToken)
        {
            int? censusPasses = null;
            IReadOnlyCollection<string> buckets = new[] { CensusBuckets.All };
            if (kind == ImportKind.Check)
            {
                var official = await _officialSite.GetOfficialCensus(mix, userId, sid, cancellationToken);
                var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
                    .ToDictionary(c => c.Id);
                // Read after the import's own saves, straight from the Ledger — the run is comparing
                // against what it just wrote.
                var records = await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix), cancellationToken);
                var local = LocalCensusBuilder.Build(mix, records, charts, official.Buckets.Keys.ToArray());

                censusPasses = official.TotalPasses;
                buckets = CensusDiff.BucketsToRepair(CensusDiff.Compare(official, local));
                if (buckets.Count == 0) return (0, official.TotalPasses);

                await _mediator.Publish(new ImportStatusUpdatedEvent(userId, "Reading the levels that don't match",
                    Array.Empty<RecordedPhoenixScore>(), mix), cancellationToken);
            }

            // The player's broken-scores choice, the same one the import above read: a repair that
            // ignored it would skip exactly the charts their imports save.
            var found = await _officialSite.GetBestScoresIn(mix, userId, sid, buckets, includeBroken,
                cancellationToken);
            var added = await SaveBests(userId, mix, sessionId, found.ToArray(), saves, cancellationToken);
            return (added, censusPasses ?? found.Count);
        }

        /// <summary>
        ///     The run's one announcement: what its saves changed, and the titles piugame showed on the
        ///     account. The Ledger decides whether that is a score event carrying the titles or, when no
        ///     score changed, the titles on their own.
        /// </summary>
        private Task Announce(Guid userId, MixEnum mix, Guid sessionId, IReadOnlyList<ScoreSaveResult> saves,
            IReadOnlyList<string> titles, CancellationToken cancellationToken)
        {
            return _mediator.Send(new AnnounceScoreChangesCommand(userId, mix, sessionId, saves, titles),
                cancellationToken);
        }

        /// <summary>
        ///     Saves whatever a scrape found that beats what we already hold, reporting progress as it
        ///     goes and adding what each save changed to <paramref name="saves" /> the moment it returns,
        ///     so a failure part-way still leaves the run holding every save that landed. Both passes of
        ///     a check or a deep scan come through here, so both obey the same rule: a scrape may
        ///     only ever RAISE a record, and only by the one published policy — a second hand-written
        ///     copy of that rule is what let plate improvements drag scores down
        ///     (docs/design/score-truth-model.md). Returns how many records it wrote.
        /// </summary>
        internal async Task<int> SaveBests(Guid userId, MixEnum mix, Guid sessionId,
            OfficialRecordedScore[] scores, ICollection<ScoreSaveResult> saves, CancellationToken cancellationToken)
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
                saves.Add(await _mediator.Send(
                    new UpdatePhoenixBestAttemptCommand(score.Chart.Id, score.IsBroken, score.Score, score.Plate,
                        KeepBestStats: true,
                        Source: ScoreJournalEntry.OfficialImportSource, Mix: mix,
                        SessionId: sessionId,
                        RecordedAt: score.RecordedAt,
                        Judgements: score.Judgements,
                        // The import already knows: a chart in existingScores is one we held a
                        // record on, so this card raised it rather than being the first we ever
                        // saw. The seasonal counting rule turns on exactly that (D15).
                        RaisedExistingRecord: existingScores.ContainsKey(score.Chart.Id),
                        // The run announces once it has saved everything.
                        DeferAnnouncement: true),
                    cancellationToken));
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

            // The remainder rides a progress status of its own. "Charts finished saving" is the run's to
            // send, once, after its announcement — a check saves in two passes and the page must not read
            // the first as the end.
            if (batch.Count > 0)
                await _mediator.Publish(
                    new ImportStatusUpdatedEvent(userId, $"Saving chart result {count} of {toSave.Length}",
                        batch.ToArray(), mix),
                    cancellationToken);
            return toSave.Length;
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
