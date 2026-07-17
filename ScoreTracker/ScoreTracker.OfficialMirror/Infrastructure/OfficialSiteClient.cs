using ScoreTracker.OfficialMirror.Domain;
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
    private readonly IOfficialLeaderboardRepository _leaderboards;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IDailyStepReader _dailyStep;
    private readonly PiuGameConfiguration _configuration;

    public OfficialSiteClient(IPiuGameApi piuGame, IChartRepository charts, ILogger<OfficialSiteClient> logger,
        IMediator mediator,
        ICurrentUserAccessor currentUser,
        IScoreReader phoenixRecords, IFileUploadClient fileUpload,
        IOfficialLeaderboardRepository leaderboards,
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
        _leaderboards = leaderboards;
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

    public async Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(MixEnum mix,
        CancellationToken cancellationToken)
    {
        // The Phoenix 2 chart LIST is login-gated (the individual boards are public).
        var listClient = mix == MixEnum.Phoenix2 ? await GetServiceClient(mix, cancellationToken) : null;
        var songs = new List<PiuGameGetSongsResult.SongDto>();
        var page = 1;
        while (true)
        {
            var nextPage = await _piuGame.Get20AboveSongs(mix, page, cancellationToken, listClient);
            songs.AddRange(nextPage.Results);
            if (nextPage.IsEnd) break;

            page++;
        }

        var current = 1;
        var max = songs.Count;
        var result = new List<OfficialChartLeaderboardEntry>();
        var misMatched = new List<string>();
        foreach (var song in songs)
        {
            var chartType = Enum.Parse<ChartType>(song.Type);
            if (!DifficultyLevel.IsValid(song.Difficulty)) continue;
            var chart = (await _charts.GetChartsForSong(mix, song.Name, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == song.Difficulty);
            if (chart == null)
            {
                misMatched.Add(song.Name + " " + song.Type + " " + song.Difficulty);
                continue;
            }

            _logger.LogInformation($"Song {current++} out of {max}");
            var scores = await _piuGame.GetSongLeaderboard(mix, song.Id, cancellationToken);
            foreach (var score in scores.Results)
                result.Add(new OfficialChartLeaderboardEntry(score.ProfileName, chart, score.Score,
                    await ConvertPiuGameAvatarToPiuScoresAvatar(score.AvatarUrl, cancellationToken)
                    ?? DefaultAvatar));
        }

        return result;
    }

    public async Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken)
    {
        if (mix == MixEnum.Phoenix2) return await GetPumbilityLeaderboardEntries(mix, cancellationToken);

        var leaderboardList = await _piuGame.GetLeaderboards(mix, cancellationToken);
        var result = new List<UserOfficialLeaderboard>();
        foreach (var leaderboard in leaderboardList.Entries)
        {
            var entries = await _piuGame.GetLeaderboard(mix, leaderboard.Id, cancellationToken);
            var place = 1;
            foreach (var entry in entries.Entries.OrderByDescending(e => e.Rating))
            {
                result.Add(new UserOfficialLeaderboard(entry.ProfileName, place, "Rating", leaderboard.Name,
                    entry.Rating));
                place++;
            }
        }

        return result;
    }

    /// <summary>
    ///     Phoenix 2 replaced the per-level rating boards with one daily PUMBILITY board —
    ///     its All/Single/Double tabs ARE the mix's "Rating" leaderboards. The site clamps
    ///     out-of-range pages to the last one, so paging stops on the end markers or the
    ///     first page that adds nothing new.
    /// </summary>
    private async Task<IEnumerable<UserOfficialLeaderboard>> GetPumbilityLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var client = await GetServiceClient(mix, cancellationToken);
        var result = new List<UserOfficialLeaderboard>();
        foreach (var (chartType, boardName) in new (ChartType?, string)[]
                 {
                     (null, "PUMBILITY"),
                     (ChartType.Single, "PUMBILITY Singles"),
                     (ChartType.Double, "PUMBILITY Doubles")
                 })
        {
            var seen = new HashSet<string>();
            var place = 1;
            for (var page = 1;; page++)
            {
                var board = await _piuGame.GetPumbilityRankings(mix, chartType, page, client, cancellationToken);
                var added = 0;
                foreach (var entry in board.Entries)
                {
                    if (!seen.Add(entry.ProfileName)) continue;

                    result.Add(new UserOfficialLeaderboard(entry.ProfileName, place++, "Rating", boardName,
                        (int)entry.Pumbility));
                    added++;
                }

                if (board.IsEnd || added == 0) break;
            }

            _logger.LogInformation("{Board}: {Count} ranked players", boardName, place - 1);
        }

        return result;
    }

    private static readonly Uri DefaultAvatar =
        new("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png");

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

    /// <summary>
    ///     Mirrors the official site's avatar onto the piuimages CDN. Returns null when
    ///     the scraped URL doesn't carry a recognizable file — persisting anything in
    ///     that case wrote the bare /avatars/ directory URL over players' good avatars
    ///     (the sporadic broken-avatar bug). Callers treat null as "keep what you have".
    /// </summary>
    private async Task<Uri?> ConvertPiuGameAvatarToPiuScoresAvatar(Uri avatar, CancellationToken cancellationToken)
    {
        var match = ImageRegex.Match(avatar.ToString());
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value)) return null;
        var path = $"/avatars/{HttpUtility.UrlEncode(match.Groups[1].Value)}";
        if (!await _fileUpload.DoesFileExist(path, out var imagePath, cancellationToken))
            imagePath = await _fileUpload.CopyFromSource(avatar, path, cancellationToken);

        return imagePath;
    }

    public async Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(MixEnum mix, Guid userId,
        string sid, string id,
        bool includeBroken,
        int? maxPages, DateTimeOffset? since, CancellationToken cancellationToken)
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
            ? await WalkDatedBestScores(mix, userId, sessionId, firstPage, since, cancellationToken)
            : await WalkClassicBestScores(mix, userId, sessionId, firstPage, maxPages, cancellationToken);

        var results = new Dictionary<Guid, OfficialRecordedScore>();
        foreach (var response in responses)
        {
            var chartType = response.ChartType;


            var song = await GetMappedName(response.SongName, cancellationToken);

            var chart = (await _charts.GetChartsForSong(mix, song, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == response.Level);
            if (chart == null) continue;

            // The redesigned best list includes stage-failed bests (no plate, real partial
            // score) — they honor the same opt-in as recent-play breaks.
            if (response.IsBroken && !includeBroken) continue;

            // A chart surfacing twice in one walk (its score changed mid-walk) keeps the
            // newest-dated card; undated cards keep the classic last-wins overwrite.
            if (results.TryGetValue(chart.Id, out var alreadyMapped) &&
                alreadyMapped.RecordedAt >= response.RecordedAt) continue;

            results[chart.Id] = new OfficialRecordedScore(chart, response.Score, response.Plate,
                response.IsBroken, response.RecordedAt);
        }

        var recent = (await _piuGame.GetRecentScores(mix, sessionId, cancellationToken)).ToArray();
        // Daily Step's Limbo Day needs the lowest PASSING recent score — data the best-only
        // ScoreImportCompletedEvent can't carry, but the raw recent plays here can. Read today's
        // daily chart id(s) once, then emit a targeted observation for a matching chart below.
        var dailyChartIds = (await _dailyStep.GetCurrentChartIds(mix, cancellationToken)).ToHashSet();
        var entries = new List<ScoreImportCompletedEvent.ImportedScore>();
        foreach (var songGroup in recent.GroupBy(s => s.SongName))
        {
            var songName = await GetMappedName(songGroup.Key, cancellationToken);

            var chartDict =
                (await _charts.GetChartsForSong(mix, songName, cancellationToken)).ToArray();
            foreach (var chartGroup in songGroup.GroupBy(g => (g.Level, g.ChartType)))
            {
                var chart = chartDict.FirstOrDefault(c =>
                    c.Level == chartGroup.Key.Level && c.Type == chartGroup.Key.ChartType);
                if (chart == null) continue;

                if (chart.NoteCount == null)
                    await _charts.UpdateNoteCount(chart.Id, chartGroup.First().NoteCount, cancellationToken);

                var bestScore = chartGroup.Max(s => s.Score);
                var bestPlate = chartGroup.Max(s => s.Plate);
                var isBroken = chartGroup.All(s => s.IsBroken);
                entries.Add(new ScoreImportCompletedEvent.ImportedScore(chart.Id, bestScore, bestPlate.ToString(), isBroken));

                if (dailyChartIds.Contains(chart.Id))
                {
                    // Best feeds a normal-day board; lowest passing feeds a Limbo-day board — the
                    // WeeklyChallenge consumer picks which. Null lowest-pass = no recent run passed.
                    var lowestPass = chartGroup.Where(s => !s.IsBroken).OrderBy(s => (int)s.Score)
                        .FirstOrDefault();
                    await _bus.Publish(new DailyStepScoreObservedEvent(userId, mix, chart.Id,
                        (int)bestScore, bestPlate.ToString(), isBroken,
                        lowestPass == null ? (int?)null : (int)lowestPass.Score,
                        lowestPass?.Plate.ToString()), cancellationToken);
                }

                if (includeBroken && !results.ContainsKey(chart.Id))
                    results[chart.Id] = new OfficialRecordedScore(chart, bestScore, bestPlate, isBroken);

                // A recent play whose chart, score, and broken-ness match the best being saved
                // is the play that produced it: its judgement breakdown — and its timestamp,
                // when the best list carried none — ride onto the record.
                if (results.TryGetValue(chart.Id, out var saved))
                {
                    var producing = chartGroup
                        .Where(s => s.Score == saved.Score && s.IsBroken == saved.IsBroken)
                        .OrderByDescending(s => s.RecordedAt ?? DateTimeOffset.MinValue)
                        .FirstOrDefault();
                    if (producing != null)
                        results[chart.Id] = saved with
                        {
                            Judgements = new JudgementCounts(producing.Perfects, producing.Greats,
                                producing.Goods, producing.Bads, producing.Misses),
                            RecordedAt = saved.RecordedAt ?? producing.RecordedAt
                        };
                }
            }
        }

        await _bus.Publish(ScoreImportCompletedEvent.Create(_dateTime.Now,
                ScoreImportCompletedEvent.OfficialImportSource, userId, mix, entries.ToArray()),
            cancellationToken);
        return results.Values;
    }

    /// <summary>
    ///     Walks the redesigned (dated, newest-first) best list. Stops after the page that
    ///     crosses the watermark — that page still processes whole, so equal-timestamp saves
    ///     re-import harmlessly — or when a page adds nothing new. The site clamps
    ///     out-of-range page numbers to the last page, so repetition is the reliable end
    ///     signal and the pager markup is never trusted.
    /// </summary>
    private async Task<List<PiuGameGetBestScoresResult.ScoreDto>> WalkDatedBestScores(MixEnum mix, Guid userId,
        HttpClient sessionId, PiuGameGetBestScoresResult firstPage, DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var responses = new List<PiuGameGetBestScoresResult.ScoreDto>();
        var seen = new HashSet<(string, ChartType, int, int, DateTimeOffset?)>();
        var page = firstPage;
        for (var pageNumber = 1; pageNumber <= 1000; pageNumber++)
        {
            await _mediator.Publish(
                new ImportStatusUpdatedEvent(userId, $"Reading page {pageNumber} (Best Scores)",
                    Array.Empty<RecordedPhoenixScore>(), mix),
                cancellationToken);
            var added = 0;
            foreach (var score in page.Scores)
            {
                if (!seen.Add((score.SongName.ToString(), score.ChartType, (int)score.Level, (int)score.Score,
                        score.RecordedAt))) continue;

                responses.Add(score);
                added++;
            }

            var crossedWatermark = since != null &&
                                   page.Scores.Any(s => s.RecordedAt != null && s.RecordedAt < since);
            if (crossedWatermark || added == 0 || page.Scores.Length == 0) break;

            page = await _piuGame.GetBestScores(mix, sessionId, pageNumber + 1, cancellationToken);
        }

        return responses;
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

    public async Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> GetRecentScores(
        MixEnum mix, string username, string password, CancellationToken cancellationToken)
    {
        var session = (await _piuGame.GetSessionId(mix, username, password, cancellationToken)).client;
        var account = await _piuGame.GetAccountData(mix, session, cancellationToken);
        ThrowIfAccountInvalid(account);
        var results = (await _piuGame.GetRecentScores(mix, session, cancellationToken)).Reverse().ToArray();
        var result = new List<OfficialRecordedScore>();
        var nonMapped = new List<string>();
        var songCharts =
            (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).GroupBy(c => c.Song.Name)
            .ToDictionary(g => g.Key, g => g.ToArray());

        foreach (var record in results)
        {
            var songName = await GetMappedName(record.SongName, cancellationToken);

            var charts =
                songCharts.TryGetValue(songName, out var r) ? r : Array.Empty<Chart>();

            var chart = charts.FirstOrDefault(c => c.Type == record.ChartType && c.Level == record.Level);
            if (chart == null)
            {
                nonMapped.Add(record.SongName + " " + record.ChartType.GetShortHand() + record.Level);
                continue;
            }

            result.Add(new OfficialRecordedScore(chart, record.Score, record.Plate, record.IsBroken));
        }

        return (result, nonMapped);
    }


    // Loosened from the pinned "https://piugame.com/.../file.png?v=" form: Phoenix 2's
    // markup varies the host, extension, and query, and a miss must never fabricate an
    // empty filename.
    private readonly Regex ImageRegex = new(@"avatar_img\/([A-Za-z0-9_\-]+\.[A-Za-z]{3,4})",
        RegexOptions.Compiled);

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

    public async Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var missingCharts = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var page = 0;
        var apiResults = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        while (true)
        {
            _logger.LogInformation($"Pulling page {page}");
            var nextResult = await _piuGame.GetChartPopularityLeaderboard(mix, page, cancellationToken);
            apiResults.AddRange(nextResult.Entries);
            if (nextResult.Entries.Length < 50) break;

            page += 50;
        }

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

        var existing = result.Select(r => r.Chart.Id).Distinct().ToHashSet();
        var chartIds =
            (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var doesntExist = chartIds.Values.Where(c => !existing.Contains(c.Id)).ToArray();
        return result;
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
                null, new HashSet<Skill>()), entry.Description);
    }

    public async Task FixAvatars()
    {
        var avatars = await _leaderboards.GetUserAvatars(CancellationToken.None);
        var groups = avatars.GroupBy(a => a.AvatarPath);
        foreach (var group in groups)
        {
            var newPath = await ConvertPiuGameAvatarToPiuScoresAvatar(group.Key, CancellationToken.None);
            if (newPath == null) continue;
            await _leaderboards.UpdateAllAvatarPaths(group.Key, newPath, CancellationToken.None);
        }
    }
}
