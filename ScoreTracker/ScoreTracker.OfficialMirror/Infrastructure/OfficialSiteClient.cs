using ScoreTracker.OfficialMirror.Domain;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using ScoreTracker.Domain.Exceptions;
using System.Text.RegularExpressions;
using System.Web;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.OfficialMirror.Wiring;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class OfficialSiteClient : IOfficialSiteClient
{
    private readonly IPiuGameApi _piuGame;
    private readonly IChartRepository _charts;
    private readonly ILogger _logger;
    private readonly IMediator _mediator;
    private readonly IBus _bus;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreReader _phoenixRecords;
    private readonly IFileUploadClient _fileUpload;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IDailyStepReader _dailyStep;
    private readonly PiuGameConfiguration _configuration;

    public OfficialSiteClient(IPiuGameApi piuGame, IChartRepository charts, ILogger<OfficialSiteClient> logger,
        IMediator mediator,
        ICurrentUserAccessor currentUser,
        IScoreReader phoenixRecords, IFileUploadClient fileUpload,
        IBus bus,
        IDateTimeOffsetAccessor dateTime,
        IDailyStepReader dailyStep,
        IOptions<PiuGameConfiguration> configuration)
    {
        _piuGame = piuGame;
        _charts = charts;
        _logger = logger;
        _mediator = mediator;
        _currentUser = currentUser;
        _phoenixRecords = phoenixRecords;
        _fileUpload = fileUpload;
        _bus = bus;
        _dateTime = dateTime;
        _dailyStep = dailyStep;
        _configuration = configuration.Value;
    }

    /// <summary>
    ///     Unlike the Phoenix mirror (fully anonymous), piugame.com serves no anonymous
    ///     ranking traffic — Phoenix 2 sweeps authenticate once per call with the
    ///     configured service account.
    /// </summary>
    private async Task<HttpClient> GetServiceClient(MixEnum mix, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.ServiceUsername) ||
            string.IsNullOrWhiteSpace(_configuration.ServicePassword))
            throw new InvalidOperationException(
                "The Phoenix 2 leaderboards are login-gated: configure PiuGame:ServiceUsername and " +
                "PiuGame:ServicePassword (a dedicated service account) to run this import.");

        var (client, _) = await _piuGame.GetSessionId(mix, _configuration.ServiceUsername,
            _configuration.ServicePassword, cancellationToken);
        return client;
    }

    public async IAsyncEnumerable<OfficialChartBoardResult> GetOfficialChartBoards(MixEnum mix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The whole Phoenix 2 leaderboard area is login-gated; Phoenix stays anonymous.
        var listClient = mix == MixEnum.Phoenix2 ? await GetServiceClient(mix, cancellationToken) : null;
        var songs = new List<PiuGameGetSongsResult.SongDto>();
        var page = 1;
        while (true)
        {
            var nextPage = await _piuGame.Get20AboveSongs(mix, page, cancellationToken, listClient);
            songs.AddRange(nextPage.Results);
            if (nextPage.IsEnd) break;

            page++;
            await SweepDelay(cancellationToken);
        }

        // One blob-existence check per unique avatar per sweep — boards repeat the same
        // player art hundreds of times, and at 300-deep boards the per-row check was the
        // sweep's hidden cost.
        var avatarCache = new Dictionary<string, Uri?>();
        var total = songs.Count;
        var index = 0;
        foreach (var song in songs)
        {
            index++;
            if (!DifficultyLevel.IsValid(song.Difficulty))
            {
                yield return new OfficialChartBoardResult(index, total, null,
                    $"unparsable level: {song.Name} {song.Type} {song.Difficulty}",
                    Array.Empty<OfficialChartLeaderboardEntry>(),
                    new MissingChartSighting(song.Name, song.Type, song.Difficulty));
                continue;
            }

            var chartType = Enum.Parse<ChartType>(song.Type);
            var chart = (await _charts.GetChartsForSong(mix, song.Name, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == song.Difficulty);
            if (chart == null)
            {
                yield return new OfficialChartBoardResult(index, total, null,
                    $"no catalog chart: {song.Name} {song.Type} {song.Difficulty}",
                    Array.Empty<OfficialChartLeaderboardEntry>(),
                    new MissingChartSighting(song.Name, song.Type, song.Difficulty));
                continue;
            }

            _logger.LogInformation("Board {Index}/{Total}: {Song}", index, total, song.Name);
            // Boards deeper than one page (Phoenix 2's top 300) walk the same next/last-icon
            // protocol as the PUMBILITY board; a page that adds nothing new also stops the
            // walk since the site clamps out-of-range pages to the last one.
            var entries = new List<OfficialChartLeaderboardEntry>();
            string? failure = null;
            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var boardPage = 1;; boardPage++)
                {
                    var scores = await _piuGame.GetSongLeaderboard(mix, song.Id, boardPage, cancellationToken,
                        listClient);
                    var added = 0;
                    foreach (var score in scores.Results)
                    {
                        if (!seen.Add(score.ProfileName)) continue;

                        // Null flows on a parse miss rather than being replaced by the stock
                        // avatar: EnsurePlayers writes any non-null incoming picture, so
                        // substituting here overwrote a good mirrored avatar with a placeholder.
                        // The rating-board path below already let null through; this is the two
                        // paths agreeing.
                        entries.Add(new OfficialChartLeaderboardEntry(score.ProfileName, chart, score.Score,
                            await MirrorAvatar(score.AvatarUrl, avatarCache, cancellationToken)));
                        added++;
                    }

                    if (scores.IsEnd || added == 0) break;

                    await SweepDelay(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // One unreachable board is a skipped board, never a dead sweep.
                _logger.LogWarning(e, "Board fetch failed for {Song} {Type} {Level}", song.Name, song.Type,
                    song.Difficulty);
                failure = $"fetch failed: {e.Message}";
            }

            yield return failure == null
                ? new OfficialChartBoardResult(index, total, chart, null, entries)
                : new OfficialChartBoardResult(index, total, chart, failure,
                    Array.Empty<OfficialChartLeaderboardEntry>());

            await SweepDelay(cancellationToken);
        }
    }

    public async Task<IEnumerable<RatingBoardEntry>> GetRatingBoards(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var result = new List<RatingBoardEntry>(await GetPumbilityRatingBoards(mix, cancellationToken));
        if (mix == MixEnum.Phoenix2) return result;

        // Phoenix kept its per-level rating lists when the PUMBILITY board arrived, so it
        // publishes both. Phoenix 2 dropped them.
        var leaderboardList = await _piuGame.GetLeaderboards(mix, cancellationToken);
        foreach (var leaderboard in leaderboardList.Entries)
        {
            var entries = await _piuGame.GetLeaderboard(mix, leaderboard.Id, cancellationToken);
            result.AddRange(entries.Entries.Select(e =>
                new RatingBoardEntry(leaderboard.Name, e.ProfileName, e.Rating)));
            await SweepDelay(cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     The mix's PUMBILITY board(s). Phoenix 2 splits it into All/Single/Double tabs and
    ///     keeps decimal cents; Phoenix serves ONE whole-number board that ignores the tab
    ///     parameter, so asking for its Single and Double tabs would store three copies of
    ///     the same board under names the rankings view would then read as per-type boards.
    ///     Paging stops on the end markers or the first page that adds nothing new — the site
    ///     clamps an out-of-range page to the last one. Phoenix 2's whole leaderboard area is
    ///     login-gated; Phoenix serves the board to an anonymous session.
    /// </summary>
    private async Task<IEnumerable<RatingBoardEntry>> GetPumbilityRatingBoards(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var client = mix == MixEnum.Phoenix2 ? await GetServiceClient(mix, cancellationToken) : null;
        var result = new List<RatingBoardEntry>();
        var boards = mix == MixEnum.Phoenix2
            ? new (ChartType?, string)[]
            {
                (null, "PUMBILITY"),
                (ChartType.Single, "PUMBILITY Singles"),
                (ChartType.Double, "PUMBILITY Doubles")
            }
            : new (ChartType?, string)[] { (null, "PUMBILITY") };
        // These pages carry an avatar per row — the only avatar source outside the board
        // sweep, and far cheaper than it. Mirrored once per unique URL for the whole scrape.
        var avatarCache = new Dictionary<string, Uri?>();
        foreach (var (chartType, boardName) in boards)
        {
            var seen = new HashSet<string>();
            var count = 0;
            for (var page = 1;; page++)
            {
                var board = await _piuGame.GetPumbilityRankings(mix, chartType, page, client, cancellationToken);
                var added = 0;
                foreach (var entry in board.Entries)
                {
                    if (!seen.Add(entry.ProfileName)) continue;

                    result.Add(new RatingBoardEntry(boardName, entry.ProfileName, (decimal)entry.Pumbility,
                        entry.AvatarUrl == null
                            ? null
                            : await MirrorAvatar(entry.AvatarUrl, avatarCache, cancellationToken)));
                    added++;
                    count++;
                }

                if (board.IsEnd || added == 0) break;

                await SweepDelay(cancellationToken);
            }

            _logger.LogInformation("{Board}: {Count} ranked players", boardName, count);
        }

        return result;
    }

    private async Task<Uri?> MirrorAvatar(Uri avatar, IDictionary<string, Uri?> cache,
        CancellationToken cancellationToken)
    {
        var key = avatar.ToString();
        if (cache.TryGetValue(key, out var mirrored)) return mirrored;

        mirrored = await ConvertPiuGameAvatarToPiuScoresAvatar(avatar, cancellationToken);
        cache[key] = mirrored;
        return mirrored;
    }

    private Task SweepDelay(CancellationToken cancellationToken)
    {
        return _configuration.SweepRequestDelayMilliseconds <= 0
            ? Task.CompletedTask
            : Task.Delay(_configuration.SweepRequestDelayMilliseconds, cancellationToken);
    }

    public async Task<string> SignIn(MixEnum mix, string username, string password,
        CancellationToken cancellationToken)
    {
        return (await _piuGame.GetSessionId(mix, username, password, cancellationToken)).sid;
    }

    public async Task<int> GetScorePageCount(MixEnum mix, string sid, CancellationToken cancellationToken)
    {
        var sessionId = _piuGame.ClientForSid(mix, sid);
        var response = await _piuGame.GetBestScores(mix, sessionId, 0, cancellationToken);
        return response.MaxPage;
    }

    public async Task<AccountCensus> GetOfficialCensus(MixEnum mix, Guid userId, string sid,
        CancellationToken cancellationToken)
    {
        var client = _piuGame.ClientForSid(mix, sid);
        await Status(userId, mix, "Reading your play data", cancellationToken);

        // The landing page states which buckets this mix offers; assuming a set the site did not
        // serve is how a census silently compares different denominators.
        var landing = await _piuGame.GetPlayData(mix, client, CensusBuckets.All, cancellationToken);
        var buckets = new Dictionary<string, CensusBucket>(StringComparer.Ordinal);
        var offered = CensusBuckets.Partitioning(landing.Buckets);

        var read = 0;
        foreach (var bucket in offered)
        {
            var page = await _piuGame.GetPlayData(mix, client, bucket, cancellationToken);
            buckets[bucket] = new CensusBucket(bucket, page.Passes, page.GradeCounts, page.PlateCounts,
                page.CatalogTotal);
            await Status(userId, mix, $"Reading your play data ({++read} of {offered.Count})", cancellationToken);
        }

        await AddSubTenResidual(mix, client, landing, buckets, cancellationToken);

        // The pool page is live; the ranking board is a daily 01:00 KST batch and would report a
        // player who played today as mismatched against their own scores.
        var pumbility = await _piuGame.GetPumbility(mix, client, cancellationToken);
        await MirrorPumbilityBadge(mix, pumbility, cancellationToken);
        return new AccountCensus(mix, buckets, pumbility.Total);
    }

    /// <summary>
    ///     Mirrors the importer's PUMBILITY badge into <c>pumbility/p2</c> the first time anyone
    ///     wears an index we do not hold — the same self-heal the avatars use — and logs the
    ///     (pool, badge) pair either way: importers below the board's floor are the only
    ///     observations that can ever confirm the derived rungs
    ///     (docs/design/pumbility-levels.md §6). Never fails the read it rides on.
    /// </summary>
    private async Task MirrorPumbilityBadge(MixEnum mix, PiuGameGetPumbilityResult pumbility,
        CancellationToken cancellationToken)
    {
        if (mix != MixEnum.Phoenix2 || pumbility.BadgeIndex is not { } index ||
            pumbility.BadgeImageUrl == null) return;

        try
        {
            _logger.LogInformation("PumbilityBadgeObservation Index={BadgeIndex} Pool={Pool}", index,
                pumbility.Total);

            // Ours pad uniformly; the source's padding flips at ten, which is why the copy uses
            // the page's own URL rather than rebuilding the name.
            var path = $"/pumbility/p2/pumbility_{index:00}.png";
            if (await _fileUpload.DoesFileExist(path, out _, cancellationToken)) return;

            await _fileUpload.CopyFromSource(pumbility.BadgeImageUrl, path, cancellationToken);
            _logger.LogInformation("Mirrored pumbility badge {BadgeIndex} from {Source}", index,
                pumbility.BadgeImageUrl);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(e, "Pumbility badge mirror failed; the read continues");
        }
    }

    // A repair walks a level to its end rather than stopping on an up-score window: the window is
    // what let the chart go missing, and a level is small enough to read whole. The clamp only
    // catches a runaway — the site re-serves its last page for an out-of-range number, and a page
    // that adds nothing new ends the walk.
    private const int MaxRepairPagesPerBucket = 400;

    public async Task<IReadOnlyList<OfficialRecordedScore>> GetBestScoresIn(MixEnum mix, Guid userId, string sid,
        IReadOnlyCollection<string> buckets, bool includeBroken, CancellationToken cancellationToken)
    {
        var client = _piuGame.ClientForSid(mix, sid);
        var cards = new List<PiuGameGetBestScoresResult.ScoreDto>();
        var walked = buckets.Count == 0 ? new[] { CensusBuckets.All } : buckets.ToArray();

        foreach (var bucket in walked)
        {
            var seen = new HashSet<(string, ChartType, int, int, DateTimeOffset?)>();
            for (var page = 1; page <= MaxRepairPagesPerBucket; page++)
            {
                await Status(userId, mix, PageStatus(bucket, page), cancellationToken);
                var result = await _piuGame.GetBestScores(mix, client, page, cancellationToken, bucket);
                var added = 0;
                foreach (var card in result.Scores)
                {
                    if (!seen.Add((card.SongName.ToString(), card.ChartType, (int)card.Level, (int)card.Score,
                            card.RecordedAt))) continue;
                    cards.Add(card);
                    added++;
                }

                if (added == 0 || result.Scores.Length == 0) break;
            }
        }

        return (await MapBestList(mix, cards, includeBroken, cancellationToken)).Bests.Values.ToArray();
    }

    private static string PageStatus(string bucket, int page)
    {
        return bucket == CensusBuckets.All
            ? $"Reading every page of your best scores (page {page})"
            : $"Re-reading level {bucket} (page {page})";
    }

    /// <summary>
    ///     Phoenix's play-data page starts at level 10 and says so, so its sub-10 clears exist only
    ///     inside the best-score list's total. Phoenix 2 offers a bucket for every level from 1 and
    ///     needs neither the request nor the arithmetic — and its best list counts stage breaks,
    ///     which would make the same subtraction wrong there.
    /// </summary>
    private async Task AddSubTenResidual(MixEnum mix, HttpClient client, PiuGameGetPlayDataResult landing,
        IDictionary<string, CensusBucket> buckets, CancellationToken cancellationToken)
    {
        var lowest = landing.Buckets
            .Where(b => int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(b => int.Parse(b, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(1)
            .Min();
        if (lowest <= 1) return;

        var best = await _piuGame.GetBestScores(mix, client, 1, cancellationToken);
        if (best.TotalCharts == null) return;

        var residual = best.TotalCharts.Value - buckets.Values.Sum(b => b.Passes);
        if (residual <= 0) return;

        buckets[CensusBuckets.SubTen] = CensusBucket.Empty(CensusBuckets.SubTen) with { Passes = residual };
    }

    private Task Status(Guid userId, MixEnum mix, string status, CancellationToken cancellationToken)
    {
        return _mediator.Publish(
            new ImportStatusUpdatedEvent(userId, status, Array.Empty<RecordedPhoenixScore>(), mix),
            cancellationToken);
    }

    /// <summary>
    ///     Mirrors the official site's avatar onto the piuimages CDN. Returns null when
    ///     the scraped URL doesn't carry a recognizable file — persisting anything in
    ///     that case wrote the bare /avatars/ directory URL over players' good avatars
    ///     (the sporadic broken-avatar bug). Callers treat null as "keep what you have".
    ///     Phoenix 2's avatars mirror one folder down: its ids collide with Phoenix's on
    ///     entirely different art, and a shared folder would serve whichever mix imported
    ///     first to both — the same /p2/ split the site itself uses for stepballs.
    /// </summary>
    private async Task<Uri?> ConvertPiuGameAvatarToPiuScoresAvatar(Uri avatar, CancellationToken cancellationToken)
    {
        var match = ImageRegex.Match(avatar.ToString());
        var file = match.Groups["file"].Value;
        if (!match.Success || string.IsNullOrWhiteSpace(file)) return null;

        var folder = match.Groups["p2"].Success ? "avatars/p2" : "avatars";
        var path = $"/{folder}/{HttpUtility.UrlEncode(file)}";
        if (!await _fileUpload.DoesFileExist(path, out var imagePath, cancellationToken))
            imagePath = await _fileUpload.CopyFromSource(avatar, path, cancellationToken);

        return imagePath;
    }

    public async Task<ScrapedScores> GetRecordedScores(MixEnum mix, Guid userId,
        string sid, string id,
        bool includeBroken,
        int? maxPages, CancellationToken cancellationToken)
    {
        await _mediator.Publish(
            new ImportStatusUpdatedEvent(userId, "Logging In",
                Array.Empty<RecordedPhoenixScore>(), mix), cancellationToken);
        var sessionId = _piuGame.ClientForSid(mix, sid);

        var gameCards = await _piuGame.GetCards(mix, sessionId, cancellationToken);
        var activeCard = gameCards.FirstOrDefault(c => c.IsActive);
        if (activeCard != null && activeCard.Id != id) await _piuGame.SetCard(mix, sessionId, id, cancellationToken);

        var accountInfo = await _piuGame.GetAccountData(mix, sessionId, cancellationToken);

        var firstPage = await _piuGame.GetBestScores(mix, sessionId, 1, cancellationToken);
        // The redesigned best list dates every card and sorts newest-first, which carries the
        // incremental cutoff; the classic list has no dates and keeps the page-count delta +
        // up-score-window walk. Strategy follows the page shape, never the mix.
        var responses = firstPage.Scores.Any(s => s.RecordedAt != null)
            ? await WalkDatedBestScores(mix, userId, sessionId, firstPage, cancellationToken)
            : await WalkClassicBestScores(mix, userId, sessionId, firstPage, maxPages, cancellationToken);

        var (results, listStageBreaks) = await MapBestList(mix, responses, includeBroken, cancellationToken);
        var recentPlays = await ResolveRecentPlays(mix, sessionId, cancellationToken);

        await LearnNoteCounts(mix, recentPlays, cancellationToken);
        await ObserveScoring(mix, sessionId, recentPlays, cancellationToken);
        await AnnounceDailySteps(mix, userId, recentPlays, cancellationToken);
        EnrichBestsFromRecentPlays(recentPlays, results, includeBroken);

        // A chart whose whole recent window is stage breaks has no best to announce.
        var entries = recentPlays
            .Select(p => (p.Chart, Best: BestOf(p.Plays)))
            .Where(x => x.Best != null)
            .Select(x => new ScoreImportCompletedEvent.ImportedScore(x.Chart.Id, x.Best!.Score!.Value,
                x.Best.Plate?.ToString(), x.Best.IsBroken))
            .ToArray();
        // Every dated play is journal history, best or not — stage breaks included, from both
        // surfaces. Undated ones are skipped: the site's play time IS the row's identity, and
        // without it a re-import would duplicate the window.
        var observed = recentPlays
            .SelectMany(p => p.Plays.Where(s => s.RecordedAt != null)
                .Select(s => new RecordObservedPlaysCommand.ObservedPlay(p.Chart.Id, s.Score, s.Plate,
                    s.IsBroken, s.RecordedAt!.Value, JudgementsOf(s), s.IsStageBroken)))
            .Concat(listStageBreaks)
            .ToArray();

        await _bus.Publish(ScoreImportCompletedEvent.Create(_dateTime.Now,
                ScoreImportCompletedEvent.OfficialImportSource, userId, mix, entries.ToArray()),
            cancellationToken);
        return new ScrapedScores(results.Values.ToArray(), observed);
    }

    private static JudgementCounts JudgementsOf(PiuGameGetRecentScoresResult play)
    {
        return new JudgementCounts(play.Perfects, play.Greats, play.Goods, play.Bads, play.Misses);
    }

    /// <summary>One chart's recently-played attempts, walk-offs already removed.</summary>
    private sealed record ChartPlays(Chart Chart, PiuGameGetRecentScoresResult[] Plays);

    /// <summary>
    ///     ONE play wins a chart's recent window, and its score, plate and broken flag travel
    ///     together. Taking the best of each column independently produced attempts nobody
    ///     played — a higher break's score wearing a lower pass's cleared flag. A stage break is
    ///     never in the running (BestAttemptPolicy.CanBeRecord), so a window that holds nothing
    ///     else has no best: null.
    /// </summary>
    private static PiuGameGetRecentScoresResult? BestOf(IEnumerable<PiuGameGetRecentScoresResult> plays)
    {
        PiuGameGetRecentScoresResult? winner = null;
        foreach (var next in plays.Where(p => BestAttemptPolicy.CanBeRecord(p.IsStageBroken)))
            if (winner == null || BestAttemptPolicy.Beats(winner.Score, winner.Plate, winner.IsBroken,
                    next.Score, next.Plate, next.IsBroken))
                winner = next;

        return winner;
    }

    /// <summary>
    ///     What the best list maps to: the bests themselves, keyed by chart, and the stage breaks
    ///     the redesigned list keeps as an unpassed chart's first attempt — history to journal, never
    ///     a record. Those carry the card's date and nothing else: no judgements (the list prints
    ///     none) and no score (the number on the card is the running score at the moment the stage
    ///     broke, which is not a chart score).
    /// </summary>
    private sealed record MappedBestList(Dictionary<Guid, OfficialRecordedScore> Bests,
        IReadOnlyList<RecordObservedPlaysCommand.ObservedPlay> StageBreaks);

    /// <summary>
    ///     My Best Scores is the source of truth for the record (score-truth-model.md D3): this
    ///     maps its cards onto charts, and nothing here consults the recent window.
    /// </summary>
    private async Task<MappedBestList> MapBestList(MixEnum mix,
        IEnumerable<PiuGameGetBestScoresResult.ScoreDto> responses, bool includeBroken,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<Guid, OfficialRecordedScore>();
        var stageBreaks = new List<RecordObservedPlaysCommand.ObservedPlay>();
        foreach (var response in responses)
        {
            var song = await GetMappedName(response.SongName, cancellationToken);
            var chart = (await _charts.GetChartsForSong(mix, song, cancellationToken))
                .FirstOrDefault(c => c.Type == response.ChartType && c.Level == response.Level);
            if (chart == null) continue;

            // Someone started the song and let it fail out. The site lists those; we never
            // store one.
            if (BestAttemptPolicy.IsWalkOff(response.IsBroken, response.Score, null)) continue;

            // A stage break is never a best, whatever the opt-in says (stage-breaks-and-max-combo.md
            // D10). The list freezes an unpassed chart's first attempt, so a stage-broken card's
            // date IS a play's date: it is journaled as one when the card is dated, and it never
            // reaches the record. Dropping it from the bests is also what lets the recent window
            // seat a finished fail on the chart under the opt-in.
            if (!BestAttemptPolicy.CanBeRecord(response.IsStageBroken))
            {
                if (response.RecordedAt is { } playedAt)
                    stageBreaks.Add(new RecordObservedPlaysCommand.ObservedPlay(chart.Id, null, null, true,
                        playedAt, null, IsStageBroken: true));
                continue;
            }

            // The redesigned best list includes failed-but-finished bests (no plate, x_ grade,
            // real score) — they honor the same opt-in as recent-play breaks.
            if (response.IsBroken && !includeBroken) continue;

            // A chart surfacing twice in one walk (its score changed mid-walk) keeps the
            // newest-dated card; undated cards keep the classic last-wins overwrite.
            if (results.TryGetValue(chart.Id, out var alreadyMapped) &&
                alreadyMapped.RecordedAt >= response.RecordedAt) continue;

            results[chart.Id] = new OfficialRecordedScore(chart, response.Score,
                BestAttemptPolicy.PlateFor(response.IsBroken, response.Plate),
                response.IsBroken, response.RecordedAt);
        }

        return new MappedBestList(results, stageBreaks);
    }

    /// <summary>
    ///     Reads the recently-played window and resolves each group onto a catalog chart,
    ///     dropping walk-offs — a break that judged nothing is neither a storable score nor a
    ///     usable note-count sample.
    /// </summary>
    private async Task<IReadOnlyList<ChartPlays>> ResolveRecentPlays(MixEnum mix, HttpClient sessionId,
        CancellationToken cancellationToken)
    {
        var recent = (await _piuGame.GetRecentScores(mix, sessionId, cancellationToken)).ToArray();
        var resolved = new List<ChartPlays>();
        foreach (var songGroup in recent.GroupBy(s => s.SongName))
        {
            var songName = await GetMappedName(songGroup.Key, cancellationToken);
            var chartDict = (await _charts.GetChartsForSong(mix, songName, cancellationToken)).ToArray();
            foreach (var chartGroup in songGroup.GroupBy(g => (g.Level, g.ChartType)))
            {
                var chart = chartDict.FirstOrDefault(c =>
                    c.Level == chartGroup.Key.Level && c.Type == chartGroup.Key.ChartType);
                if (chart == null) continue;

                // Stage breaks stay: they are plays, and the journal wants them. Only the
                // walk-off — a break with nothing hit — goes.
                var plays = chartGroup
                    .Where(s => !BestAttemptPolicy.IsWalkOff(s.IsBroken, s.Score, JudgementsOf(s)))
                    .ToArray();
                if (plays.Length == 0) continue;

                resolved.Add(new ChartPlays(chart, plays));
            }
        }

        return resolved;
    }

    /// <summary>
    ///     Only a PASS judges every note — a break's counts stop where the stage did, so they are
    ///     always short of the chart's real total. The catalog learns a note count once and never
    ///     revisits it, so a partial one sticks forever; with no passing play in the window we
    ///     leave it for a later import instead of guessing.
    /// </summary>
    private async Task LearnNoteCounts(MixEnum mix, IReadOnlyList<ChartPlays> recentPlays,
        CancellationToken cancellationToken)
    {
        foreach (var (chart, plays) in recentPlays)
        {
            if (chart.NoteCount != null) continue;

            var passed = plays.FirstOrDefault(s => !s.IsBroken);
            if (passed == null) continue;

            await _charts.UpdateNoteCount(mix, chart.Id, passed.NoteCount, cancellationToken);
        }
    }

    /// <summary>
    ///     Temporary instrumentation (2026-08-08) — see <see cref="ScoringObservations" />. The
    ///     whole body is guarded because none of what it collects is worth a failed import: by
    ///     this point the player's scores have already been scraped, and throwing here would
    ///     lose them.
    ///     <para>
    ///         The PUMBILITY read is one extra GET of a page the player can open themselves, on
    ///         the session this import already holds. It is fetched here rather than reused from
    ///         the census because the census only runs when someone presses Score Check, which
    ///         is far too rare to accumulate the cells that are missing.
    ///     </para>
    ///     <para>
    ///         Phoenix 2 only, both halves. Phoenix 1's per-chart PUMBILITY reconciled exactly
    ///         and prices no plate at all, so a residual there could only restate what is
    ///         already known, and its grade cutoffs have been settled since launch. Watching
    ///         Phoenix 1 for a contradicting grade would be a reasonable tripwire in permanent
    ///         code, but this is not permanent, and two days is far too short a window for a
    ///         tripwire to earn anything. What it would cost is unbounded: Phoenix 1 carries an
    ///         order of magnitude more importers, so if it ever did fire it would fire on the
    ///         majority path — and because sampling is scoped to the whole operation, a flood of
    ///         Phoenix 1 operations would crowd out the Phoenix 2 rows this exists to collect.
    ///     </para>
    /// </summary>
    private async Task ObserveScoring(MixEnum mix, HttpClient sessionId,
        IReadOnlyList<ChartPlays> recentPlays, CancellationToken cancellationToken)
    {
        // Phoenix 2 and nothing else, so a Phoenix 1 import does literally none of this. Gating
        // here rather than inside the detector also means PumbilityScoring is never handed a mix
        // it has no formula for, instead of relying on the catch to absorb it.
        if (mix != MixEnum.Phoenix2) return;

        try
        {
            ScoringObservations.ObserveGrades(_logger, mix, recentPlays.SelectMany(p => p.Plays));

            var pumbility = await _piuGame.GetPumbility(mix, sessionId, cancellationToken);
            ScoringObservations.ObservePumbility(_logger, mix, pumbility.Entries);
            await MirrorPumbilityBadge(mix, pumbility, cancellationToken);
        }
        // Filtering on the token rather than the exception type: a cancelled import does not
        // reliably surface as OperationCanceledException, and swallowing a cancellation here
        // would let the import carry on as though nothing had been asked of it.
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(e, "Scoring observation failed; the import continues");
        }
    }

    /// <summary>
    ///     Daily Step's Limbo Day needs the lowest PASSING recent score — data the best-only
    ///     ScoreImportCompletedEvent can't carry, but the raw recent plays can. Best feeds a
    ///     normal-day board, lowest passing feeds a Limbo-day one; the WeeklyChallenge consumer
    ///     picks which. Null lowest-pass = no recent run passed.
    /// </summary>
    private async Task AnnounceDailySteps(MixEnum mix, Guid userId, IReadOnlyList<ChartPlays> recentPlays,
        CancellationToken cancellationToken)
    {
        var dailyChartIds = (await _dailyStep.GetCurrentChartIds(mix, cancellationToken)).ToHashSet();
        foreach (var (chart, plays) in recentPlays.Where(p => dailyChartIds.Contains(p.Chart.Id)))
        {
            // A window of nothing but stage breaks observed no score for the board.
            var best = BestOf(plays);
            if (best == null) continue;

            var lowestPass = plays.Where(s => !s.IsBroken).OrderBy(s => (int)s.Score!.Value).FirstOrDefault();
            await _bus.Publish(new DailyStepScoreObservedEvent(userId, mix, chart.Id,
                (int)best.Score!.Value, best.Plate?.ToString(), best.IsBroken,
                lowestPass == null ? (int?)null : (int)lowestPass.Score!.Value,
                lowestPass?.Plate?.ToString()), cancellationToken);
        }
    }

    /// <summary>
    ///     What the recent window contributes to the RECORD, which the best list otherwise owns:
    ///     a broken best for a chart the best page never listed (opt-in only); a better finished
    ///     fail than a broken card — the list freezes an unpassed chart's first attempt, so the
    ///     window often holds a higher one, and broken may replace broken through the ordinary
    ///     policy while a passing card is never touched from here (stage-breaks-and-max-combo.md
    ///     D17); and the judgement breakdown — plus the timestamp, when the best list carried none
    ///     — of the play that produced whatever is being saved.
    /// </summary>
    private static void EnrichBestsFromRecentPlays(IReadOnlyList<ChartPlays> recentPlays,
        IDictionary<Guid, OfficialRecordedScore> results, bool includeBroken)
    {
        foreach (var (chart, plays) in recentPlays)
        {
            var best = BestOf(plays);
            if (best != null)
            {
                if (!results.ContainsKey(chart.Id))
                {
                    if (includeBroken)
                        results[chart.Id] = new OfficialRecordedScore(chart, best.Score!.Value, best.Plate, best.IsBroken);
                }
                else if (results[chart.Id] is { IsBroken: true } card && BestAttemptPolicy.Beats(card.Score, card.Plate,
                             card.IsBroken, best.Score, best.Plate, best.IsBroken))
                {
                    results[chart.Id] = new OfficialRecordedScore(chart, best.Score!.Value, best.Plate, best.IsBroken);
                }
            }

            if (!results.TryGetValue(chart.Id, out var saved)) continue;

            // A recent play whose chart, score and broken-ness match the best being saved IS the
            // play that produced it.
            var producing = plays
                .Where(s => s.Score == saved.Score && s.IsBroken == saved.IsBroken)
                .OrderByDescending(s => s.RecordedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();
            if (producing == null) continue;

            // The producing play's own time WINS over the best card's stamp. The card's date is
            // not when that score was set: it is stamped when the chart first reaches the list and
            // never moves again, so a chart failed on the 12th and passed on the 14th still shows
            // the 12th beside the passing score (measured against the live site, 2026-08-18 —
            // docs/design/stage-breaks-and-max-combo.md §6). Taking the card's date made every such
            // pass collide with the earlier attempt's journal row, which is one play's key holding
            // another play's result. The card's stamp is the fallback, for a best the recent window
            // no longer reaches.
            results[chart.Id] = saved with
            {
                Judgements = JudgementsOf(producing),
                RecordedAt = producing.RecordedAt ?? saved.RecordedAt
            };
        }
    }

    // Five consecutive best pages holding nothing new-or-improved end the dated walk — the
    // port of the classic "five folders back" up-score window (~12 cards a page, so a
    // ~60-chart look-back). Any page with even one savable card resets the count.
    private const int MaxDatedPagesWithoutNewBest = 5;

    /// <summary>
    ///     Walks the redesigned (newest-played-first) best list, collecting every card for the
    ///     caller to best-filter. Stops on the up-score window — <see cref="MaxDatedPagesWithoutNewBest" />
    ///     consecutive pages holding nothing we don't already have at an equal-or-better
    ///     result — never on the card's displayed date. On the redesign that date is the
    ///     chart's FIRST play, unrelated to the newest-played sort order, so trusting it as a
    ///     cutoff ended the walk a page in whenever a replayed old chart sat near the top (the
    ///     bug that truncated every incremental import after the first). A page that adds
    ///     nothing is also the reliable end-of-list signal, since the site clamps out-of-range
    ///     page numbers to the last page and its pager markup is never trusted.
    /// </summary>
    private async Task<List<PiuGameGetBestScoresResult.ScoreDto>> WalkDatedBestScores(MixEnum mix, Guid userId,
        HttpClient sessionId, PiuGameGetBestScoresResult firstPage,
        CancellationToken cancellationToken)
    {
        var responses = new List<PiuGameGetBestScoresResult.ScoreDto>();
        var seen = new HashSet<(string, ChartType, int, int, DateTimeOffset?)>();
        var storedBests = (await _phoenixRecords.GetBestScores(mix, userId, cancellationToken))
            .ToDictionary(r => r.ChartId);
        var pagesWithoutNewBest = 0;
        var page = firstPage;
        for (var pageNumber = 1; pageNumber <= 1000; pageNumber++)
        {
            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId, $"Reading page {pageNumber} (Best Scores)",
                    Array.Empty<RecordedPhoenixScore>(), mix),
                cancellationToken);
            var added = 0;
            var newBests = 0;
            foreach (var score in page.Scores)
            {
                if (!seen.Add((score.SongName.ToString(), score.ChartType, (int)score.Level, (int)score.Score,
                        score.RecordedAt))) continue;

                responses.Add(score);
                added++;
                if (await IsNewOrImprovedBest(mix, score, storedBests, cancellationToken)) newBests++;
            }

            // A page that adds nothing is the end of the list (or the clamp re-serving a page
            // we've already read): stop regardless of the window.
            if (added == 0 || page.Scores.Length == 0) break;

            pagesWithoutNewBest = newBests == 0 ? pagesWithoutNewBest + 1 : 0;
            if (pagesWithoutNewBest >= MaxDatedPagesWithoutNewBest) break;

            page = await _piuGame.GetBestScores(mix, sessionId, pageNumber + 1, cancellationToken);
        }

        return responses;
    }

    /// <summary>
    ///     Whether a best card would change what we store — a chart we don't hold yet, a
    ///     stage break we've since cleared, or a better plate/score at the same broken-ness.
    ///     Mirrors the saga's save filter so the dated walk pages exactly as far as there is
    ///     new work to save, and no farther. An unmappable card is never "work".
    /// </summary>
    private async Task<bool> IsNewOrImprovedBest(MixEnum mix, PiuGameGetBestScoresResult.ScoreDto card,
        IReadOnlyDictionary<Guid, RecordedPhoenixScore> storedBests, CancellationToken cancellationToken)
    {
        var song = await GetMappedName(card.SongName, cancellationToken);
        var chart = (await _charts.GetChartsForSong(mix, song, cancellationToken))
            .FirstOrDefault(c => c.Type == card.ChartType && c.Level == card.Level);
        if (chart == null) return false;
        if (BestAttemptPolicy.IsWalkOff(card.IsBroken, card.Score, null)) return false;
        // A stage break is history, never a best: it seats nothing, so it is not work — otherwise
        // every stage break on the list would keep the walk going.
        if (!BestAttemptPolicy.CanBeRecord(card.IsStageBroken)) return false;

        return BestAttemptPolicy.Beats(storedBests.GetValueOrDefault(chart.Id), card.Score,
            BestAttemptPolicy.PlateFor(card.IsBroken, card.Plate), card.IsBroken);
    }

    private async Task<List<PiuGameGetBestScoresResult.ScoreDto>> WalkClassicBestScores(MixEnum mix, Guid userId,
        HttpClient sessionId, PiuGameGetBestScoresResult firstPage, int? maxPages,
        CancellationToken cancellationToken)
    {
        var finalPage = firstPage.MaxPage;
        maxPages ??= finalPage;
        var responses = new List<PiuGameGetBestScoresResult.ScoreDto>();
        var currentPage = 1;
        var page = firstPage;
        while (currentPage <= maxPages.Value)
        {
            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId, $"Reading page {currentPage} of {maxPages} (New Passes)",
                    Array.Empty<RecordedPhoenixScore>(), mix),
                cancellationToken);
            if (currentPage > 1) page = await _piuGame.GetBestScores(mix, sessionId, currentPage, cancellationToken);
            responses.AddRange(page.Scores);
            currentPage++;
            _logger.LogInformation($"Page {currentPage}");
        }

        var pagesWithNoUpscore = 0;
        var bestScores =
            (await _phoenixRecords.GetBestScores(mix, userId, cancellationToken))
            .ToDictionary(r =>
                r.ChartId);
        while (pagesWithNoUpscore <= 3 && currentPage <= finalPage)
        {
            pagesWithNoUpscore++;
            var nextPage = await _piuGame.GetBestScores(mix, sessionId, currentPage, cancellationToken);
            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId, $"Reading page {currentPage} (Up-scores)",
                    Array.Empty<RecordedPhoenixScore>(), mix),
                cancellationToken);

            foreach (var score in nextPage.Scores)
            {
                var song = await GetMappedName(score.SongName, cancellationToken);

                var chart = (await _charts.GetChartsForSong(mix, song, cancellationToken))
                    .FirstOrDefault(c => c.Type == score.ChartType && c.Level == score.Level);
                if (chart == null) continue;
                if (bestScores.ContainsKey(chart.Id) && score.Score <= (bestScores[chart.Id].Score ?? 0)) continue;

                responses.Add(score);
                pagesWithNoUpscore = 0;
            }

            currentPage++;
        }

        return responses;
    }

    // Loosened from the pinned "https://piugame.com/.../file.png?v=" form: Phoenix 2's
    // markup varies the host, extension, and query, and a miss must never fabricate an
    // empty filename. The optional trailing "2" is load-bearing twice over: Phoenix 2
    // serves avatars from /data/avatar_img2/, and without it every P2 avatar missed the
    // match and fell back to the default art; and the two directories REUSE ids for
    // unrelated pictures (verified 2026-07-26 — avatar_img/9516….png and
    // avatar_img2/9516….png are different images), so the capture also decides which
    // mirror folder the file belongs in.
    private readonly Regex ImageRegex =
        new(@"avatar_img(?<p2>2)?\/(?<file>[A-Za-z0-9_\-]+\.[A-Za-z]{3,4})", RegexOptions.Compiled);

    public async Task<PiuGameAccountDataImport> GetAccountData(MixEnum mix, string sid, string? id,
        CancellationToken cancellationToken)
    {
        var client = _piuGame.ClientForSid(mix, sid);

        if (id != null) await _piuGame.SetCard(mix, client, id, cancellationToken);

        var importedData = await _piuGame.GetAccountData(mix, client, cancellationToken);
        ThrowIfAccountInvalid(importedData);
        var imagePath = await ConvertPiuGameAvatarToPiuScoresAvatar(importedData.ImageUrl, cancellationToken);
        var titles = importedData.TitleEntries.Where(t => t.Have).Select(t =>
            t.Name + (t.Name.ToString().Contains("GAMER") || t.Name == "LOVERS"
                ? t.ColClass switch
                {
                    "col1" => " (Platinum)",
                    "col2" => " (Gold)",
                    "col3" => " (Silver)",
                    "col4" => " (Bronze)",
                    _ => ""
                }
                : "")).Select(Name.From).ToArray();
        return new PiuGameAccountDataImport(imagePath, importedData.AccountName, titles, sid);
    }

    public async Task<IEnumerable<GameCardRecord>> GetGameCards(MixEnum mix, string sid,
        CancellationToken cancellationToken)
    {
        var session = _piuGame.ClientForSid(mix, sid);
        var account = await _piuGame.GetAccountData(mix, session, cancellationToken);
        ThrowIfAccountInvalid(account);
        return await _piuGame.GetCards(mix, session, cancellationToken);
    }

    public async Task<Contracts.PiuGameAccountIdentity> GetAccountIdentity(MixEnum mix, string username,
        string password,
        CancellationToken cancellationToken)
    {
        var session = (await _piuGame.GetSessionId(mix, username, password, cancellationToken)).client;
        var account = await _piuGame.GetAccountData(mix, session, cancellationToken);
        ThrowIfAccountInvalid(account);
        var cards = (await _piuGame.GetCards(mix, session, cancellationToken)).ToArray();
        var imagePath = await ConvertPiuGameAvatarToPiuScoresAvatar(account.ImageUrl, cancellationToken);
        return new Contracts.PiuGameAccountIdentity(username, account.AccountName, imagePath, cards);
    }

    /// <summary>
    ///     "INVALID" is the parser's sentinel for a my_page that carries no profile data. Two
    ///     very different truths produce it: the session isn't authenticated at all (wrong
    ///     credentials — the site serves its login page), or the login worked but the account
    ///     has no game profile/card associated yet — everyone's launch-week state on Phoenix 2.
    ///     Conflating them told P2 users "wrong password" when their password was fine.
    /// </summary>
    private static void ThrowIfAccountInvalid(PiuGameGetAccountDataResult account)
    {
        if (account.AccountName != "INVALID") return;
        if (account.RequiresLogin) throw new InvalidCredentialException("Invalid username or password");
        throw new NoGameAccountAssociatedException();
    }

    private async Task<string> GetMappedName(string songName, CancellationToken cancellationToken)
    {
        songName = songName.Trim();
        var cultureMapping = await _charts.GetEnglishLookup("ko-KR", cancellationToken);
        if (ManualMappings.TryGetValue(songName, out var mapping)) songName = mapping;

        if (cultureMapping.TryGetValue(songName, out var value)) songName = value;

        return songName;
    }

    private static readonly IDictionary<string, string> ManualMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Kasou Shinja仮装信者", "Kasou Shinja" },
            { "Re：End of a Dream", "Re:End of a Dream" },
            { "CROSS RAY (feat. 月下Lia)", "Cross Ray" },
            { "ヨロピク ピクヨロ！", "Yoropiku Pikuyoro !" },
            { "甘い誘惑デインジャラス", "Amai Yuuwaku Dangerous" },
            { "甘い誘惑デインジャラス\nAmai Yuuwaku Dangerous", "Amai Yuuwaku Dangerous" },
            { "ヨロピク ピクヨロ！\nYoropiku Pikuyoro !", "Yoropiku Pikuyoro !" }
        };

    public async Task<(IReadOnlyList<ChartPopularityLeaderboardEntry> Entries,
        IReadOnlyList<MissingChartSighting> Missing)> GetOfficialChartLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken)
    {
        // The whole Phoenix 2 leaderboard area is login-gated, the play ranking included;
        // Phoenix stays anonymous.
        var client = mix == MixEnum.Phoenix2 ? await GetServiceClient(mix, cancellationToken) : null;
        var missingCharts = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var page = 0;
        var rawRows = 0;
        var apiResults = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        while (true)
        {
            _logger.LogInformation($"Pulling page {page}");
            var nextResult = await _piuGame.GetChartPopularityLeaderboard(mix, page, _dateTime.Now,
                cancellationToken, client);
            apiResults.AddRange(nextResult.Entries);
            rawRows += nextResult.RawRowCount;
            // The walk ends when the SITE serves a short page — parsed counts can dip
            // under 50 on a full page (skipped tiles) without meaning the ranking ended.
            if (nextResult.RawRowCount < 50) break;

            page += 50;
            await SweepDelay(cancellationToken);
        }

        _logger.LogInformation(
            "Popularity walk {Mix}: {Pages} pages, {Raw} raw tiles, {Parsed} parsed",
            mix, page / 50 + 1, rawRows, apiResults.Count);

        var result = new List<ChartPopularityLeaderboardEntry>();
        foreach (var apiResult in apiResults)
        {
            var song = await GetMappedName(apiResult.SongName, cancellationToken);

            var charts = (await _charts.GetChartsForSong(mix, song, cancellationToken)).ToArray();
            var chart = charts
                .FirstOrDefault(c => c.Level == apiResult.ChartLevel && c.Type == apiResult.ChartType);

            if (chart == null)
            {
                missingCharts.Add(apiResult);
                continue;
            }

            result.Add(new ChartPopularityLeaderboardEntry(chart, apiResult.Place,
                new Uri(apiResult.SongImage, UriKind.Absolute)));
        }

        // The counts tell a field test what actually happened: zero scraped means the
        // page served nothing (auth, date, or markup drift), while a large unmapped tally
        // means the catalog is missing content.
        _logger.LogInformation("Popularity {Mix}: {Mapped} charts ranked, {Unmapped} unmapped",
            mix, result.Count, missingCharts.Count);
        return (result, missingCharts
            .Select(m => new MissingChartSighting(m.SongName.ToString(), m.ChartType.ToString(),
                (int)m.ChartLevel))
            .ToArray());
    }

    public async Task<PiuGameUcsEntry?> GetUcs(int id, CancellationToken cancellationToken)
    {
        var entry = await _piuGame.GetUcs(id, cancellationToken);
        if (entry == null) return null;

        var songName = await GetMappedName(entry.SongName, cancellationToken);
        var song = (await _charts.GetChartsForSong(MixEnum.Phoenix, songName, cancellationToken))
            .FirstOrDefault()?.Song;
        if (song == null) return null;

        return new PiuGameUcsEntry(id,
            new Chart(new Guid(), MixEnum.Phoenix, song, entry.ChartType, entry.Level, MixEnum.Phoenix, entry.Uploader,
                null), entry.Description);
    }
}
