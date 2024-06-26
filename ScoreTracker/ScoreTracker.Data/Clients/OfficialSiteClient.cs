using System.Security.Authentication;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Application.Commands;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Clients;

public sealed class OfficialSiteClient : IOfficialSiteClient
{
    private readonly IPiuGameApi _piuGame;
    private readonly IChartRepository _charts;
    private readonly ILogger _logger;
    private readonly IMediator _mediator;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPhoenixRecordRepository _phoenixRecords;
    private readonly IFileUploadClient _fileUpload;
    private readonly IOfficialLeaderboardRepository _leaderboards;
    private readonly IWeeklyTournamentRepository _weeklyTournies;

    public OfficialSiteClient(IPiuGameApi piuGame, IChartRepository charts, ILogger<OfficialSiteClient> logger,
        IMediator mediator,
        ICurrentUserAccessor currentUser,
        IPhoenixRecordRepository phoenixRecords, IFileUploadClient fileUpload,
        IOfficialLeaderboardRepository leaderboards,
        IWeeklyTournamentRepository weeklyTournies)
    {
        _piuGame = piuGame;
        _charts = charts;
        _logger = logger;
        _mediator = mediator;
        _currentUser = currentUser;
        _phoenixRecords = phoenixRecords;
        _fileUpload = fileUpload;
        _leaderboards = leaderboards;
        _weeklyTournies = weeklyTournies;
    }

    public async Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(
        CancellationToken cancellationToken)
    {
        var songs = new List<PiuGameGetSongsResult.SongDto>();
        var page = 1;
        while (true)
        {
            var nextPage = await _piuGame.Get20AboveSongs(page, cancellationToken);
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
            var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song.Name, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == song.Difficulty);
            if (chart == null)
            {
                misMatched.Add(song.Name + " " + song.Type + " " + song.Difficulty);
                continue;
            }

            _logger.LogInformation($"Song {current++} out of {max}");
            var scores = await _piuGame.GetSongLeaderboard(song.Id, cancellationToken);
            foreach (var score in scores.Results)
                result.Add(new OfficialChartLeaderboardEntry(score.ProfileName, chart, score.Score,
                    await ConvertPiuGameAvatarToPiuScoresAvatar(score.AvatarUrl, cancellationToken)));
        }

        return result;
    }

    public async Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(CancellationToken cancellationToken)
    {
        var leaderboardList = await _piuGame.GetLeaderboards(cancellationToken);
        var result = new List<UserOfficialLeaderboard>();
        foreach (var leaderboard in leaderboardList.Entries)
        {
            var entries = await _piuGame.GetLeaderboard(leaderboard.Id, cancellationToken);
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

    public async Task<int> GetScorePageCount(string username, string password, CancellationToken cancellationToken)
    {
        var sessionId = await _piuGame.GetSessionId(username, password, cancellationToken);
        var response = await _piuGame.GetBestScores(sessionId, 0, cancellationToken);
        return response.MaxPage;
    }

    private async Task<Uri> ConvertPiuGameAvatarToPiuScoresAvatar(Uri avatar, CancellationToken cancellationToken)
    {
        var file = ImageRegex.Match(avatar.ToString()).Groups[1].Value;
        var path = $"/avatars/{file}";
        if (!await _fileUpload.DoesFileExist(path, out var imagePath, cancellationToken))
            imagePath = await _fileUpload.CopyFromSource(avatar, path, cancellationToken);

        return imagePath;
    }

    public async Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(Guid userId, string username,
        string password,
        bool includeBroken,
        int? maxPages, CancellationToken cancellationToken)
    {
        var currentPage = 1;
        await _mediator.Publish(
            new ImportStatusUpdated(_currentUser.User.Id, "Logging In",
                Array.Empty<RecordedPhoenixScore>()), cancellationToken);
        var sessionId = await _piuGame.GetSessionId(username, password, cancellationToken);

        var finalPage = (await _piuGame.GetBestScores(sessionId, 1, cancellationToken)).MaxPage;
        var responses = new List<PiuGameGetBestScoresResult.ScoreDto>();
        maxPages ??= finalPage;
        while (currentPage <= maxPages.Value)
        {
            await _mediator.Publish(
                new ImportStatusUpdated(_currentUser.User.Id, $"Reading page {currentPage} of {maxPages} (New Passes)",
                    Array.Empty<RecordedPhoenixScore>()),
                cancellationToken);
            var nextPage = await _piuGame.GetBestScores(sessionId, currentPage, cancellationToken);
            responses.AddRange(nextPage.Scores);
            currentPage++;
            _logger.LogInformation($"Page {currentPage}");
        }

        var pagesWithNoUpscore = 0;
        var bestScores =
            (await _phoenixRecords.GetRecordedScores(_currentUser.User.Id, cancellationToken)).ToDictionary(r =>
                r.ChartId);
        while (pagesWithNoUpscore <= 3 && currentPage <= finalPage)
        {
            pagesWithNoUpscore++;
            var nextPage = await _piuGame.GetBestScores(sessionId, currentPage, cancellationToken);
            await _mediator.Publish(
                new ImportStatusUpdated(_currentUser.User.Id, $"Reading page {currentPage} (Up-scores)",
                    Array.Empty<RecordedPhoenixScore>()),
                cancellationToken);

            foreach (var score in nextPage.Scores)
            {
                var song = await GetMappedName(score.SongName, cancellationToken);

                var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song, cancellationToken))
                    .FirstOrDefault(c => c.Type == score.ChartType && c.Level == score.Level);
                if (chart == null) continue;
                if (bestScores.ContainsKey(chart.Id) && score.Score <= (bestScores[chart.Id].Score ?? 0)) continue;

                responses.Add(score);
                pagesWithNoUpscore = 0;
            }

            currentPage++;
        }

        var results = new Dictionary<Guid, OfficialRecordedScore>();
        foreach (var response in responses)
        {
            var chartType = response.ChartType;


            var song = await GetMappedName(response.SongName, cancellationToken);

            var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == response.Level);
            if (chart == null) continue;

            results[chart.Id] = new OfficialRecordedScore(chart, response.Score, response.Plate);
        }

        var recent = (await _piuGame.GetRecentScores(sessionId, cancellationToken)).ToArray();
        var weeklyCharts = (await _weeklyTournies.GetWeeklyCharts(cancellationToken)).Select(e => e.ChartId).Distinct()
            .ToHashSet();
        foreach (var songGroup in recent.GroupBy(s => s.SongName))
        {
            var songName = await GetMappedName(songGroup.Key, cancellationToken);

            var chartDict =
                (await _charts.GetChartsForSong(MixEnum.Phoenix, songName, cancellationToken)).ToArray();
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
                if (weeklyCharts.Contains(chart.Id))
                    try
                    {
                        await _mediator.Send(new RegisterWeeklyChartScore(
                                new WeeklyTournamentEntry(userId, chart.Id, bestScore, bestPlate, isBroken,
                                    null, 10.0)),
                            cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e,
                            $"There was an error when registering leaderboard scores for {userId} on {chart.Song.Name} {chart.DifficultyString}");
                    }

                if (!includeBroken || results.ContainsKey(chart.Id)) continue;

                results[chart.Id] = new OfficialRecordedScore(chart, bestScore, bestPlate, isBroken);
            }
        }

        return results.Values;
    }

    public async Task<(IEnumerable<OfficialRecordedScore> results, IEnumerable<string> nonMapped)> GetRecentScores(
        string username, string password, CancellationToken cancellationToken)
    {
        var session = await _piuGame.GetSessionId(username, password, cancellationToken);
        var account = await _piuGame.GetAccountData(session, cancellationToken);
        if (account.AccountName == "INVALID") throw new InvalidCredentialException("Invalid username or password");
        var results = (await _piuGame.GetRecentScores(session, cancellationToken)).Reverse().ToArray();
        var result = new List<OfficialRecordedScore>();
        var nonMapped = new List<string>();
        var songCharts =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken)).GroupBy(c => c.Song.Name)
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

    private readonly Regex ImageRegex = new(@"https\:\/\/piugame\.com\/data\/avatar_img\/([A-Za-z0-9]+.png)\?v\=",
        RegexOptions.Compiled);

    public async Task<PiuGameAccountDataImport> GetAccountData(string username, string password,
        CancellationToken cancellationToken)
    {
        var client = await _piuGame.GetSessionId(username, password, cancellationToken);
        var importedData = await _piuGame.GetAccountData(client, cancellationToken);
        if (importedData.AccountName == "INVALID")
            throw new InvalidCredentialException("Could not log in user to PIUgame");
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
        return new PiuGameAccountDataImport(imagePath, importedData.AccountName, titles);
    }

    private async Task<string> GetMappedName(string songName, CancellationToken cancellationToken)
    {
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

    public async Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(
        CancellationToken cancellationToken)
    {
        var missingCharts = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var page = 0;
        var apiResults = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        while (true)
        {
            _logger.LogInformation($"Pulling page {page}");
            var nextResult = await _piuGame.GetChartPopularityLeaderboard(page, cancellationToken);
            apiResults.AddRange(nextResult.Entries);
            if (nextResult.Entries.Length < 50) break;

            page += 50;
        }

        var result = new List<ChartPopularityLeaderboardEntry>();
        foreach (var apiResult in apiResults)
        {
            var song = await GetMappedName(apiResult.SongName, cancellationToken);

            var charts = (await _charts.GetChartsForSong(MixEnum.Phoenix, song, cancellationToken)).ToArray();
            var chart = charts
                .FirstOrDefault(c => c.Level == apiResult.ChartLevel && c.Type == apiResult.ChartType);

            if (chart == null)
            {
                missingCharts.Add(apiResult);
                continue;
            }

            result.Add(new ChartPopularityLeaderboardEntry(chart, apiResult.Place));
        }

        var existing = result.Select(r => r.Chart.Id).Distinct().ToHashSet();
        var chartIds =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var doesntExist = chartIds.Values.Where(c => !existing.Contains(c.Id)).ToArray();
        return result;
    }

    public async Task FixAvatars()
    {
        var avatars = await _leaderboards.GetUserAvatars(CancellationToken.None);
        var groups = avatars.GroupBy(a => a.AvatarPath);
        foreach (var group in groups)
        {
            var newPath = await ConvertPiuGameAvatarToPiuScoresAvatar(group.Key, CancellationToken.None);
            await _leaderboards.UpdateAllAvatarPaths(group.Key, newPath, CancellationToken.None);
        }
    }
}
